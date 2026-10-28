namespace ValidationStage
{
    partial class F_StageDirection
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
            this._axisHeader = new System.Windows.Forms.Label();
            this._currentHeader = new System.Windows.Forms.Label();
            this._newHeader = new System.Windows.Forms.Label();
            this._xLabel = new System.Windows.Forms.Label();
            this._xCurrentLabel = new System.Windows.Forms.Label();
            this._xCombo = new System.Windows.Forms.ComboBox();
            this._yLabel = new System.Windows.Forms.Label();
            this._yCurrentLabel = new System.Windows.Forms.Label();
            this._yCombo = new System.Windows.Forms.ComboBox();
            this._zLabel = new System.Windows.Forms.Label();
            this._zCurrentLabel = new System.Windows.Forms.Label();
            this._zCombo = new System.Windows.Forms.ComboBox();
            this._noteLabel = new System.Windows.Forms.Label();
            this._readButton = new System.Windows.Forms.Button();
            this._applyButton = new System.Windows.Forms.Button();
            this._closeButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // _axisHeader
            //
            this._axisHeader.Location = new System.Drawing.Point(16, 14);
            this._axisHeader.Name = "_axisHeader";
            this._axisHeader.Size = new System.Drawing.Size(40, 20);
            this._axisHeader.TabIndex = 0;
            this._axisHeader.Text = "축";
            //
            // _currentHeader
            //
            this._currentHeader.Location = new System.Drawing.Point(60, 14);
            this._currentHeader.Name = "_currentHeader";
            this._currentHeader.Size = new System.Drawing.Size(110, 20);
            this._currentHeader.TabIndex = 1;
            this._currentHeader.Text = "현재 설정";
            //
            // _newHeader
            //
            this._newHeader.Location = new System.Drawing.Point(180, 14);
            this._newHeader.Name = "_newHeader";
            this._newHeader.Size = new System.Drawing.Size(130, 20);
            this._newHeader.TabIndex = 2;
            this._newHeader.Text = "변경할 값";
            //
            // _xLabel
            //
            this._xLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._xLabel.Location = new System.Drawing.Point(16, 42);
            this._xLabel.Name = "_xLabel";
            this._xLabel.Size = new System.Drawing.Size(40, 23);
            this._xLabel.TabIndex = 3;
            this._xLabel.Text = "X";
            //
            // _xCurrentLabel
            //
            this._xCurrentLabel.Location = new System.Drawing.Point(60, 45);
            this._xCurrentLabel.Name = "_xCurrentLabel";
            this._xCurrentLabel.Size = new System.Drawing.Size(110, 20);
            this._xCurrentLabel.TabIndex = 4;
            this._xCurrentLabel.Text = "-";
            //
            // _xCombo
            //
            this._xCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._xCombo.Items.AddRange(new object[] {
            "CCW (REV0)",
            "CW (REV1)"});
            this._xCombo.Location = new System.Drawing.Point(180, 42);
            this._xCombo.Name = "_xCombo";
            this._xCombo.Size = new System.Drawing.Size(130, 23);
            this._xCombo.TabIndex = 5;
            //
            // _yLabel
            //
            this._yLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._yLabel.Location = new System.Drawing.Point(16, 74);
            this._yLabel.Name = "_yLabel";
            this._yLabel.Size = new System.Drawing.Size(40, 23);
            this._yLabel.TabIndex = 6;
            this._yLabel.Text = "Y";
            //
            // _yCurrentLabel
            //
            this._yCurrentLabel.Location = new System.Drawing.Point(60, 77);
            this._yCurrentLabel.Name = "_yCurrentLabel";
            this._yCurrentLabel.Size = new System.Drawing.Size(110, 20);
            this._yCurrentLabel.TabIndex = 7;
            this._yCurrentLabel.Text = "-";
            //
            // _yCombo
            //
            this._yCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._yCombo.Items.AddRange(new object[] {
            "CCW (REV0)",
            "CW (REV1)"});
            this._yCombo.Location = new System.Drawing.Point(180, 74);
            this._yCombo.Name = "_yCombo";
            this._yCombo.Size = new System.Drawing.Size(130, 23);
            this._yCombo.TabIndex = 8;
            //
            // _zLabel
            //
            this._zLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._zLabel.Location = new System.Drawing.Point(16, 106);
            this._zLabel.Name = "_zLabel";
            this._zLabel.Size = new System.Drawing.Size(40, 23);
            this._zLabel.TabIndex = 9;
            this._zLabel.Text = "Z";
            //
            // _zCurrentLabel
            //
            this._zCurrentLabel.Location = new System.Drawing.Point(60, 109);
            this._zCurrentLabel.Name = "_zCurrentLabel";
            this._zCurrentLabel.Size = new System.Drawing.Size(110, 20);
            this._zCurrentLabel.TabIndex = 10;
            this._zCurrentLabel.Text = "-";
            //
            // _zCombo
            //
            this._zCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._zCombo.Items.AddRange(new object[] {
            "CCW (REV0)",
            "CW (REV1)"});
            this._zCombo.Location = new System.Drawing.Point(180, 106);
            this._zCombo.Name = "_zCombo";
            this._zCombo.Size = new System.Drawing.Size(130, 23);
            this._zCombo.TabIndex = 11;
            //
            // _noteLabel
            //
            this._noteLabel.ForeColor = System.Drawing.Color.Firebrick;
            this._noteLabel.Location = new System.Drawing.Point(16, 142);
            this._noteLabel.Name = "_noteLabel";
            this._noteLabel.Size = new System.Drawing.Size(300, 36);
            this._noteLabel.TabIndex = 12;
            this._noteLabel.Text = "컨트롤러에 저장됩니다. 바꾼 뒤에는 저속으로 양쪽 리밋에서 멈추는지 먼저 확인하세요.";
            //
            // _readButton
            //
            this._readButton.Location = new System.Drawing.Point(16, 186);
            this._readButton.Name = "_readButton";
            this._readButton.Size = new System.Drawing.Size(80, 26);
            this._readButton.TabIndex = 13;
            this._readButton.Text = "다시 읽기";
            this._readButton.UseVisualStyleBackColor = true;
            this._readButton.Click += new System.EventHandler(this.ReadButton_Click);
            //
            // _applyButton
            //
            this._applyButton.Location = new System.Drawing.Point(150, 186);
            this._applyButton.Name = "_applyButton";
            this._applyButton.Size = new System.Drawing.Size(80, 26);
            this._applyButton.TabIndex = 14;
            this._applyButton.Text = "적용";
            this._applyButton.UseVisualStyleBackColor = true;
            this._applyButton.Click += new System.EventHandler(this.ApplyButton_Click);
            //
            // _closeButton
            //
            this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._closeButton.Location = new System.Drawing.Point(236, 186);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(80, 26);
            this._closeButton.TabIndex = 15;
            this._closeButton.Text = "닫기";
            this._closeButton.UseVisualStyleBackColor = true;
            //
            // F_StageDirection
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._closeButton;
            this.ClientSize = new System.Drawing.Size(332, 226);
            this.Controls.Add(this._axisHeader);
            this.Controls.Add(this._currentHeader);
            this.Controls.Add(this._newHeader);
            this.Controls.Add(this._xLabel);
            this.Controls.Add(this._xCurrentLabel);
            this.Controls.Add(this._xCombo);
            this.Controls.Add(this._yLabel);
            this.Controls.Add(this._yCurrentLabel);
            this.Controls.Add(this._yCombo);
            this.Controls.Add(this._zLabel);
            this.Controls.Add(this._zCurrentLabel);
            this.Controls.Add(this._zCombo);
            this.Controls.Add(this._noteLabel);
            this.Controls.Add(this._readButton);
            this.Controls.Add(this._applyButton);
            this.Controls.Add(this._closeButton);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "F_StageDirection";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "스테이지 모터 방향";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _axisHeader;
        private System.Windows.Forms.Label _currentHeader;
        private System.Windows.Forms.Label _newHeader;
        private System.Windows.Forms.Label _xLabel;
        private System.Windows.Forms.Label _xCurrentLabel;
        private System.Windows.Forms.ComboBox _xCombo;
        private System.Windows.Forms.Label _yLabel;
        private System.Windows.Forms.Label _yCurrentLabel;
        private System.Windows.Forms.ComboBox _yCombo;
        private System.Windows.Forms.Label _zLabel;
        private System.Windows.Forms.Label _zCurrentLabel;
        private System.Windows.Forms.ComboBox _zCombo;
        private System.Windows.Forms.Label _noteLabel;
        private System.Windows.Forms.Button _readButton;
        private System.Windows.Forms.Button _applyButton;
        private System.Windows.Forms.Button _closeButton;
    }
}
