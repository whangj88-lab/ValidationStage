namespace ValidationStage
{
    partial class F_HexapodSoftLimit
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
            this._csLabel = new System.Windows.Forms.Label();
            this._csCombo = new System.Windows.Forms.ComboBox();
            this._activeLabel = new System.Windows.Forms.Label();
            this._enabledCheck = new System.Windows.Forms.CheckBox();
            this._axisHeader = new System.Windows.Forms.Label();
            this._minHeader = new System.Windows.Forms.Label();
            this._maxHeader = new System.Windows.Forms.Label();
            this._positionHeader = new System.Windows.Forms.Label();
            this._txLabel = new System.Windows.Forms.Label();
            this._txMinInput = new System.Windows.Forms.NumericUpDown();
            this._txMaxInput = new System.Windows.Forms.NumericUpDown();
            this._txPositionLabel = new System.Windows.Forms.Label();
            this._tyLabel = new System.Windows.Forms.Label();
            this._tyMinInput = new System.Windows.Forms.NumericUpDown();
            this._tyMaxInput = new System.Windows.Forms.NumericUpDown();
            this._tyPositionLabel = new System.Windows.Forms.Label();
            this._tzLabel = new System.Windows.Forms.Label();
            this._tzMinInput = new System.Windows.Forms.NumericUpDown();
            this._tzMaxInput = new System.Windows.Forms.NumericUpDown();
            this._tzPositionLabel = new System.Windows.Forms.Label();
            this._noteLabel = new System.Windows.Forms.Label();
            this._defaultButton = new System.Windows.Forms.Button();
            this._applyButton = new System.Windows.Forms.Button();
            this._closeButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._txMinInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._txMaxInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._tyMinInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._tyMaxInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._tzMinInput)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._tzMaxInput)).BeginInit();
            this.SuspendLayout();
            //
            // _csLabel
            //
            this._csLabel.Location = new System.Drawing.Point(16, 17);
            this._csLabel.Name = "_csLabel";
            this._csLabel.Size = new System.Drawing.Size(60, 20);
            this._csLabel.TabIndex = 0;
            this._csLabel.Text = "좌표계:";
            //
            // _csCombo
            //
            this._csCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._csCombo.Location = new System.Drawing.Point(80, 14);
            this._csCombo.Name = "_csCombo";
            this._csCombo.Size = new System.Drawing.Size(160, 23);
            this._csCombo.TabIndex = 1;
            this._csCombo.SelectedIndexChanged += new System.EventHandler(this.CsCombo_SelectedIndexChanged);
            //
            // _activeLabel
            //
            this._activeLabel.Location = new System.Drawing.Point(252, 17);
            this._activeLabel.Name = "_activeLabel";
            this._activeLabel.Size = new System.Drawing.Size(160, 20);
            this._activeLabel.TabIndex = 2;
            this._activeLabel.Text = "활성: -";
            //
            // _enabledCheck
            //
            this._enabledCheck.AutoSize = true;
            this._enabledCheck.Location = new System.Drawing.Point(16, 50);
            this._enabledCheck.Name = "_enabledCheck";
            this._enabledCheck.Size = new System.Drawing.Size(120, 19);
            this._enabledCheck.TabIndex = 3;
            this._enabledCheck.Text = "소프트 리밋 사용";
            this._enabledCheck.UseVisualStyleBackColor = true;
            //
            // _axisHeader
            //
            this._axisHeader.Location = new System.Drawing.Point(16, 82);
            this._axisHeader.Name = "_axisHeader";
            this._axisHeader.Size = new System.Drawing.Size(40, 20);
            this._axisHeader.TabIndex = 4;
            this._axisHeader.Text = "축";
            //
            // _minHeader
            //
            this._minHeader.Location = new System.Drawing.Point(60, 82);
            this._minHeader.Name = "_minHeader";
            this._minHeader.Size = new System.Drawing.Size(110, 20);
            this._minHeader.TabIndex = 5;
            this._minHeader.Text = "최소 (arcmin)";
            //
            // _maxHeader
            //
            this._maxHeader.Location = new System.Drawing.Point(180, 82);
            this._maxHeader.Name = "_maxHeader";
            this._maxHeader.Size = new System.Drawing.Size(110, 20);
            this._maxHeader.TabIndex = 6;
            this._maxHeader.Text = "최대 (arcmin)";
            //
            // _positionHeader
            //
            this._positionHeader.Location = new System.Drawing.Point(300, 82);
            this._positionHeader.Name = "_positionHeader";
            this._positionHeader.Size = new System.Drawing.Size(110, 20);
            this._positionHeader.TabIndex = 7;
            this._positionHeader.Text = "현재 위치";
            //
            // _txLabel
            //
            this._txLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._txLabel.Location = new System.Drawing.Point(16, 108);
            this._txLabel.Name = "_txLabel";
            this._txLabel.Size = new System.Drawing.Size(40, 23);
            this._txLabel.TabIndex = 8;
            this._txLabel.Text = "TX";
            //
            // _txMinInput
            //
            this._txMinInput.DecimalPlaces = 3;
            this._txMinInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._txMinInput.Location = new System.Drawing.Point(60, 108);
            this._txMinInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._txMinInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._txMinInput.Name = "_txMinInput";
            this._txMinInput.Size = new System.Drawing.Size(110, 23);
            this._txMinInput.TabIndex = 9;
            this._txMinInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _txMaxInput
            //
            this._txMaxInput.DecimalPlaces = 3;
            this._txMaxInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._txMaxInput.Location = new System.Drawing.Point(180, 108);
            this._txMaxInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._txMaxInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._txMaxInput.Name = "_txMaxInput";
            this._txMaxInput.Size = new System.Drawing.Size(110, 23);
            this._txMaxInput.TabIndex = 10;
            this._txMaxInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _txPositionLabel
            //
            this._txPositionLabel.Location = new System.Drawing.Point(300, 111);
            this._txPositionLabel.Name = "_txPositionLabel";
            this._txPositionLabel.Size = new System.Drawing.Size(110, 20);
            this._txPositionLabel.TabIndex = 11;
            this._txPositionLabel.Text = "-";
            //
            // _tyLabel
            //
            this._tyLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._tyLabel.Location = new System.Drawing.Point(16, 138);
            this._tyLabel.Name = "_tyLabel";
            this._tyLabel.Size = new System.Drawing.Size(40, 23);
            this._tyLabel.TabIndex = 12;
            this._tyLabel.Text = "TY";
            //
            // _tyMinInput
            //
            this._tyMinInput.DecimalPlaces = 3;
            this._tyMinInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._tyMinInput.Location = new System.Drawing.Point(60, 138);
            this._tyMinInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._tyMinInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._tyMinInput.Name = "_tyMinInput";
            this._tyMinInput.Size = new System.Drawing.Size(110, 23);
            this._tyMinInput.TabIndex = 13;
            this._tyMinInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _tyMaxInput
            //
            this._tyMaxInput.DecimalPlaces = 3;
            this._tyMaxInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._tyMaxInput.Location = new System.Drawing.Point(180, 138);
            this._tyMaxInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._tyMaxInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._tyMaxInput.Name = "_tyMaxInput";
            this._tyMaxInput.Size = new System.Drawing.Size(110, 23);
            this._tyMaxInput.TabIndex = 14;
            this._tyMaxInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _tyPositionLabel
            //
            this._tyPositionLabel.Location = new System.Drawing.Point(300, 141);
            this._tyPositionLabel.Name = "_tyPositionLabel";
            this._tyPositionLabel.Size = new System.Drawing.Size(110, 20);
            this._tyPositionLabel.TabIndex = 15;
            this._tyPositionLabel.Text = "-";
            //
            // _tzLabel
            //
            this._tzLabel.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold);
            this._tzLabel.Location = new System.Drawing.Point(16, 168);
            this._tzLabel.Name = "_tzLabel";
            this._tzLabel.Size = new System.Drawing.Size(40, 23);
            this._tzLabel.TabIndex = 16;
            this._tzLabel.Text = "TZ";
            //
            // _tzMinInput
            //
            this._tzMinInput.DecimalPlaces = 3;
            this._tzMinInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._tzMinInput.Location = new System.Drawing.Point(60, 168);
            this._tzMinInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._tzMinInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._tzMinInput.Name = "_tzMinInput";
            this._tzMinInput.Size = new System.Drawing.Size(110, 23);
            this._tzMinInput.TabIndex = 17;
            this._tzMinInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _tzMaxInput
            //
            this._tzMaxInput.DecimalPlaces = 3;
            this._tzMaxInput.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            this._tzMaxInput.Location = new System.Drawing.Point(180, 168);
            this._tzMaxInput.Maximum = new decimal(new int[] { 3000, 0, 0, 0 });
            this._tzMaxInput.Minimum = new decimal(new int[] { 3000, 0, 0, -2147483648 });
            this._tzMaxInput.Name = "_tzMaxInput";
            this._tzMaxInput.Size = new System.Drawing.Size(110, 23);
            this._tzMaxInput.TabIndex = 18;
            this._tzMaxInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // _tzPositionLabel
            //
            this._tzPositionLabel.Location = new System.Drawing.Point(300, 171);
            this._tzPositionLabel.Name = "_tzPositionLabel";
            this._tzPositionLabel.Size = new System.Drawing.Size(110, 20);
            this._tzPositionLabel.TabIndex = 19;
            this._tzPositionLabel.Text = "-";
            //
            // _noteLabel
            //
            this._noteLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this._noteLabel.Location = new System.Drawing.Point(16, 204);
            this._noteLabel.Name = "_noteLabel";
            this._noteLabel.Size = new System.Drawing.Size(396, 70);
            this._noteLabel.TabIndex = 20;
            this._noteLabel.Text = "단위 arcmin (메인 화면 표시값 기준). 좌표계마다 따로 저장되며, 이동/조그는 활성 좌표계의 값으로 검사합니다. 범위를 벗어나는 이동 명령은 전체를 거부하고, 조그는 리밋에서 멈춥니다. 저장한 적 없는 좌표계는 ±300 arcmin 입니다.";
            //
            // _defaultButton
            //
            this._defaultButton.Location = new System.Drawing.Point(16, 282);
            this._defaultButton.Name = "_defaultButton";
            this._defaultButton.Size = new System.Drawing.Size(100, 26);
            this._defaultButton.TabIndex = 21;
            this._defaultButton.Text = "기본값 (±300)";
            this._defaultButton.UseVisualStyleBackColor = true;
            this._defaultButton.Click += new System.EventHandler(this.DefaultButton_Click);
            //
            // _applyButton
            //
            this._applyButton.Location = new System.Drawing.Point(246, 282);
            this._applyButton.Name = "_applyButton";
            this._applyButton.Size = new System.Drawing.Size(80, 26);
            this._applyButton.TabIndex = 22;
            this._applyButton.Text = "적용";
            this._applyButton.UseVisualStyleBackColor = true;
            this._applyButton.Click += new System.EventHandler(this.ApplyButton_Click);
            //
            // _closeButton
            //
            this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._closeButton.Location = new System.Drawing.Point(332, 282);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(80, 26);
            this._closeButton.TabIndex = 23;
            this._closeButton.Text = "닫기";
            this._closeButton.UseVisualStyleBackColor = true;
            //
            // F_HexapodSoftLimit
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._closeButton;
            this.ClientSize = new System.Drawing.Size(428, 322);
            this.Controls.Add(this._csLabel);
            this.Controls.Add(this._csCombo);
            this.Controls.Add(this._activeLabel);
            this.Controls.Add(this._enabledCheck);
            this.Controls.Add(this._axisHeader);
            this.Controls.Add(this._minHeader);
            this.Controls.Add(this._maxHeader);
            this.Controls.Add(this._positionHeader);
            this.Controls.Add(this._txLabel);
            this.Controls.Add(this._txMinInput);
            this.Controls.Add(this._txMaxInput);
            this.Controls.Add(this._txPositionLabel);
            this.Controls.Add(this._tyLabel);
            this.Controls.Add(this._tyMinInput);
            this.Controls.Add(this._tyMaxInput);
            this.Controls.Add(this._tyPositionLabel);
            this.Controls.Add(this._tzLabel);
            this.Controls.Add(this._tzMinInput);
            this.Controls.Add(this._tzMaxInput);
            this.Controls.Add(this._tzPositionLabel);
            this.Controls.Add(this._noteLabel);
            this.Controls.Add(this._defaultButton);
            this.Controls.Add(this._applyButton);
            this.Controls.Add(this._closeButton);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "F_HexapodSoftLimit";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "헥사포드 소프트 리밋";
            ((System.ComponentModel.ISupportInitialize)(this._txMinInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._txMaxInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._tyMinInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._tyMaxInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._tzMinInput)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._tzMaxInput)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label _csLabel;
        private System.Windows.Forms.ComboBox _csCombo;
        private System.Windows.Forms.Label _activeLabel;
        private System.Windows.Forms.CheckBox _enabledCheck;
        private System.Windows.Forms.Label _axisHeader;
        private System.Windows.Forms.Label _minHeader;
        private System.Windows.Forms.Label _maxHeader;
        private System.Windows.Forms.Label _positionHeader;
        private System.Windows.Forms.Label _txLabel;
        private System.Windows.Forms.NumericUpDown _txMinInput;
        private System.Windows.Forms.NumericUpDown _txMaxInput;
        private System.Windows.Forms.Label _txPositionLabel;
        private System.Windows.Forms.Label _tyLabel;
        private System.Windows.Forms.NumericUpDown _tyMinInput;
        private System.Windows.Forms.NumericUpDown _tyMaxInput;
        private System.Windows.Forms.Label _tyPositionLabel;
        private System.Windows.Forms.Label _tzLabel;
        private System.Windows.Forms.NumericUpDown _tzMinInput;
        private System.Windows.Forms.NumericUpDown _tzMaxInput;
        private System.Windows.Forms.Label _tzPositionLabel;
        private System.Windows.Forms.Label _noteLabel;
        private System.Windows.Forms.Button _defaultButton;
        private System.Windows.Forms.Button _applyButton;
        private System.Windows.Forms.Button _closeButton;
    }
}
