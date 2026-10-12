using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using ValidationStage.Devices;

namespace ValidationStage
{
    /// <summary>
    /// 비전(CSH) 프로그램과의 TCP 통신. R_S@1 을 보내고 A_R 응답에서 비전 검출 오프셋(X,Y,Z,TX,TY,TZ)을 꺼낸다.
    /// 프로토콜은 CSHEmulator\CSHProtocolSample (CSHClient/CSHServer) 와 같다:
    ///   요청: "R_S@&lt;frameCount&gt;\r\n"
    ///   응답: "A_R@&lt;frameCount&gt;@" + Binary + "@\r\n"
    ///         Binary = 44 byte Header (Int64 Time, Int32 FrameCount, Double FPS/LED Left/LED Right/Test Time)
    ///                + double[frameCount] 배열 9개 (Pseudo OMM 이면 17개), Little Endian
    /// 프레임은 1개만 요청한다. 실패하면 null/false 를 돌려주고 원인은 <see cref="LastError"/> 에 남긴다.
    /// </summary>
    internal class VisionClient
    {
        public const int DefaultPort = 5000;

        // 비전 쪽 OIS 옵션과 같아야 한다 (배열 순서가 바뀐다).
        //   true : X,Y,Z, COMP_TX,COMP_TY,COMP_TZ, TX,TY,TZ
        //   false: X,Y,Z, TX,TY,TZ, COMP_TX,COMP_TY,COMP_TZ
        private const bool OisOption = false;

        private const int ReplyTimeoutMs = 10000;
        private const int HeaderSize = 44;
        private static readonly string HostFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VisionHost.txt");

        private TcpClient _tcp;
        private NetworkStream _stream;

        /// <summary>마지막 실패 원인 (로그용).</summary>
        public string LastError { get; private set; }

        /// <summary>마지막으로 연결에 성공한 주소 ("ip" 또는 "ip:port"). VisionHost.txt 에 저장된다.</summary>
        public string Host { get; private set; } = LoadHost();

        public bool IsConnected => _tcp != null && _tcp.Connected;

        /// <summary>비전 프로그램에 접속한다. host = "ip" 또는 "ip:port" (포트 생략 시 5000). 성공하면 VisionHost.txt 에 저장한다.</summary>
        public async Task<bool> ConnectAsync(string host)
        {
            Disconnect();
            try
            {
                host = host.Trim();
                string ip = host;
                int port = DefaultPort;
                int colon = host.LastIndexOf(':');
                if (colon > 0)
                {
                    ip = host.Substring(0, colon);
                    port = int.Parse(host.Substring(colon + 1));
                }

                var tcp = new TcpClient { NoDelay = true };
                await tcp.ConnectAsync(ip, port);
                _tcp = tcp;
                _stream = tcp.GetStream();
                _stream.ReadTimeout = ReplyTimeoutMs;

                Host = host;
                try { File.WriteAllText(HostFile, host); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                Disconnect();
                LastError = ex.Message;
                return false;
            }
        }

        public void Disconnect()
        {
            try { _stream?.Close(); } catch { }
            try { _tcp?.Close(); } catch { }
            _stream = null;
            _tcp = null;
        }

        /// <summary>
        /// R_S@1 을 보내고 A_R 을 받아 오프셋을 돌려준다. 실패하면 null (통신 오류/시간 초과면 연결도 끊는다 - 응답 순서가 어긋나지 않게).
        /// </summary>
        public Task<VisionOffset> RequestOffsetAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!IsConnected)
                    {
                        throw new InvalidOperationException(T("연결되어 있지 않습니다", "Not connected"));
                    }

                    // 이전에 늦게 도착한 응답이 남아 있으면 버린다.
                    var buffer = new byte[8192];
                    while (_stream.DataAvailable)
                    {
                        _stream.Read(buffer, 0, buffer.Length);
                    }

                    byte[] request = Encoding.ASCII.GetBytes("R_S@1\r\n");
                    _stream.Write(request, 0, request.Length);

                    var received = new List<byte>();
                    while (true)
                    {
                        int read = _stream.Read(buffer, 0, buffer.Length);   // ReadTimeout 이 지나면 IOException
                        if (read == 0)
                        {
                            throw new IOException(T("비전 프로그램이 연결을 끊었습니다", "The vision program closed the connection"));
                        }
                        for (int i = 0; i < read; i++)
                        {
                            received.Add(buffer[i]);
                        }

                        VisionOffset offset = TryParseAR(received.ToArray());
                        if (offset != null)
                        {
                            return offset;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Disconnect();
                    LastError = ex is IOException && ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.TimedOut
                        ? T($"A_R 응답 없음 ({ReplyTimeoutMs / 1000}초)", $"No A_R reply ({ReplyTimeoutMs / 1000} s)")
                        : ex.Message;
                    return null;
                }
            });
        }

        /// <summary>
        /// 받은 데이터에서 완성된 A_R 한 개를 찾아 첫 프레임의 값을 꺼낸다. 아직 덜 받았으면 null.
        /// A_R 앞에 다른 응답이 섞여 있으면 무시한다. Binary 안에 0D 0A 가 있을 수 있어서 CRLF 가 아니라 길이로 끝을 찾는다.
        /// </summary>
        private static VisionOffset TryParseAR(byte[] data)
        {
            int start = IndexOf(data, Encoding.ASCII.GetBytes("A_R@"), 0);
            if (start < 0)
            {
                return null;
            }
            int at2 = Array.IndexOf(data, (byte)'@', start + 4);
            if (at2 < 0)
            {
                return null;
            }
            string countText = Encoding.ASCII.GetString(data, start + 4, at2 - (start + 4));
            if (!int.TryParse(countText, out int frameCount) || frameCount <= 0)
            {
                throw new InvalidDataException(T("A_R 프레임 수 해석 실패: ", "A_R frame count parse failed: ") + countText);
            }

            int dataStart = at2 + 1;
            foreach (int arrayCount in new[] { 9, 17 })   // 일반 9개 / Pseudo OMM 17개
            {
                int tail = dataStart + HeaderSize + frameCount * sizeof(double) * arrayCount;
                if (data.Length >= tail + 3 && data[tail] == '@' && data[tail + 1] == '\r' && data[tail + 2] == '\n')
                {
                    // 배열 k 의 첫 프레임 값
                    double First(int k) => BitConverter.ToDouble(data, dataStart + HeaderSize + k * frameCount * sizeof(double));
                    int tiltIndex = OisOption ? 6 : 3;
                    return new VisionOffset
                    {
                        X = First(0),
                        Y = First(1),
                        Z = First(2),
                        TX = First(tiltIndex),
                        TY = First(tiltIndex + 1),
                        TZ = First(tiltIndex + 2),
                    };
                }
            }

            int longest = dataStart + HeaderSize + frameCount * sizeof(double) * 17 + 3;
            if (data.Length >= longest)
            {
                throw new InvalidDataException(T("A_R 형식 오류 (끝 표시 @\\r\\n 없음)", "Invalid A_R format (no @\\r\\n tail)"));
            }
            return null;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start)
        {
            for (int i = start; i <= data.Length - pattern.Length; i++)
            {
                int j = 0;
                while (j < pattern.Length && data[i + j] == pattern[j])
                {
                    j++;
                }
                if (j == pattern.Length)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string LoadHost()
        {
            try
            {
                if (File.Exists(HostFile))
                {
                    string value = File.ReadAllText(HostFile).Trim();
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }
            catch { }
            return "127.0.0.1:" + DefaultPort;
        }

        private static string T(string korean, string english)
        {
            return Messages.Language == Language.English ? english : korean;
        }
    }

    /// <summary>비전 검출 오프셋. X,Y,Z: µm / TX,TY,TZ: arcmin (비전이 보내는 값 그대로).</summary>
    internal class VisionOffset
    {
        public double X, Y, Z, TX, TY, TZ;
    }
}
