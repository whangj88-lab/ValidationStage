namespace ValidationStage
{
    partial class F_Hexapod3D
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this._host = new System.Windows.Forms.Integration.ElementHost();
            this._statusLabel = new System.Windows.Forms.Label();
            this._timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // _host
            //
            this._host.Dock = System.Windows.Forms.DockStyle.Fill;
            this._host.Location = new System.Drawing.Point(0, 0);
            this._host.Name = "_host";
            this._host.Size = new System.Drawing.Size(884, 637);
            this._host.TabIndex = 0;
            this._host.Child = null;
            //
            // _statusLabel
            //
            this._statusLabel.AutoEllipsis = true;
            this._statusLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._statusLabel.Location = new System.Drawing.Point(0, 637);
            this._statusLabel.Name = "_statusLabel";
            this._statusLabel.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this._statusLabel.Size = new System.Drawing.Size(884, 24);
            this._statusLabel.TabIndex = 1;
            this._statusLabel.Text = "-";
            this._statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // _timer
            //
            this._timer.Interval = 100;
            this._timer.Tick += new System.EventHandler(this.Timer_Tick);
            //
            // F_Hexapod3D
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 661);
            this.Controls.Add(this._host);
            this.Controls.Add(this._statusLabel);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.Name = "F_Hexapod3D";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "헥사포드 3D 보기";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.F_Hexapod3D_FormClosing);
            this.Load += new System.EventHandler(this.F_Hexapod3D_Load);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Integration.ElementHost _host;
        private System.Windows.Forms.Label _statusLabel;
        private System.Windows.Forms.Timer _timer;
    }
}
