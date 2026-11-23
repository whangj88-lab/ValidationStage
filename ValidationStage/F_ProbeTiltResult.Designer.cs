namespace ValidationStage
{
    partial class F_ProbeTiltResult
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
            this._verdictLabel = new System.Windows.Forms.Label();
            this._grid = new System.Windows.Forms.DataGridView();
            this._colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colOld = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colNew = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colSpread = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colRms = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colChangeBefore = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colChangeOld = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colChangeNew = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._colVerdict = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this._noteLabel = new System.Windows.Forms.Label();
            this._warningLabel = new System.Windows.Forms.Label();
            this._fileLabel = new System.Windows.Forms.Label();
            this._folderButton = new System.Windows.Forms.Button();
            this._applyButton = new System.Windows.Forms.Button();
            this._closeButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this._grid)).BeginInit();
            this.SuspendLayout();
            //
            // _verdictLabel
            //
            this._verdictLabel.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Bold);
            this._verdictLabel.Location = new System.Drawing.Point(12, 10);
            this._verdictLabel.Name = "_verdictLabel";
            this._verdictLabel.Size = new System.Drawing.Size(600, 26);
            this._verdictLabel.TabIndex = 0;
            this._verdictLabel.Text = "전체 판정: -";
            //
            // _grid
            //
            this._grid.AllowUserToAddRows = false;
            this._grid.AllowUserToDeleteRows = false;
            this._grid.AllowUserToResizeRows = false;
            this._grid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this._grid.ColumnHeadersHeight = 42;
            this._grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            this._grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this._colName,
            this._colOld,
            this._colNew,
            this._colSpread,
            this._colRms,
            this._colChangeBefore,
            this._colChangeOld,
            this._colChangeNew,
            this._colVerdict});
            this._grid.Location = new System.Drawing.Point(12, 42);
            this._grid.Name = "_grid";
            this._grid.ReadOnly = true;
            this._grid.RowHeadersVisible = false;
            this._grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this._grid.Size = new System.Drawing.Size(1016, 252);
            this._grid.TabIndex = 1;
            this._grid.SelectionChanged += new System.EventHandler(this.Grid_SelectionChanged);
            //
            // _colName
            //
            this._colName.FillWeight = 160F;
            this._colName.HeaderText = "항목";
            this._colName.Name = "_colName";
            this._colName.ReadOnly = true;
            //
            // _colOld
            //
            this._colOld.HeaderText = "기존 계수";
            this._colOld.Name = "_colOld";
            this._colOld.ReadOnly = true;
            //
            // _colNew
            //
            this._colNew.HeaderText = "새 계수";
            this._colNew.Name = "_colNew";
            this._colNew.ReadOnly = true;
            //
            // _colSpread
            //
            this._colSpread.HeaderText = "회차별 편차";
            this._colSpread.Name = "_colSpread";
            this._colSpread.ReadOnly = true;
            //
            // _colRms
            //
            this._colRms.HeaderText = "직선 이탈\r\nRMS (µm)";
            this._colRms.Name = "_colRms";
            this._colRms.ReadOnly = true;
            //
            // _colChangeBefore
            //
            this._colChangeBefore.HeaderText = "보정 전\r\n변화량 (µm)";
            this._colChangeBefore.Name = "_colChangeBefore";
            this._colChangeBefore.ReadOnly = true;
            //
            // _colChangeOld
            //
            this._colChangeOld.HeaderText = "기존 계수\r\n변화량 (µm)";
            this._colChangeOld.Name = "_colChangeOld";
            this._colChangeOld.ReadOnly = true;
            //
            // _colChangeNew
            //
            this._colChangeNew.HeaderText = "새 계수\r\n변화량 (µm)";
            this._colChangeNew.Name = "_colChangeNew";
            this._colChangeNew.ReadOnly = true;
            //
            // _colVerdict
            //
            this._colVerdict.FillWeight = 110F;
            this._colVerdict.HeaderText = "판정";
            this._colVerdict.Name = "_colVerdict";
            this._colVerdict.ReadOnly = true;
            //
            // _noteLabel
            //
            this._noteLabel.ForeColor = System.Drawing.SystemColors.GrayText;
            this._noteLabel.Location = new System.Drawing.Point(12, 300);
            this._noteLabel.Name = "_noteLabel";
            this._noteLabel.Size = new System.Drawing.Size(1016, 52);
            this._noteLabel.TabIndex = 2;
            this._noteLabel.Text = "변화량 = 검증 스캔에서 이동축이 3200 µm (±1600) 움직일 때 그 항목이 변한 양 (직선 기울기 기준). 0 에 가까울수록 좋습니다.\r\n판정 (새 계수 변화량): 양호 0.32 µm 이하 / 주의 0.64 µm 이하 / 불량 그보다 큼.\r\n회차별 편차나 직선 이탈 RMS 가 주황색이면 프로브 고정/접촉, 글라스 면의 이물을 확인하세요.";
            //
            // _warningLabel
            //
            this._warningLabel.ForeColor = System.Drawing.Color.Firebrick;
            this._warningLabel.Location = new System.Drawing.Point(12, 354);
            this._warningLabel.Name = "_warningLabel";
            this._warningLabel.Size = new System.Drawing.Size(1016, 36);
            this._warningLabel.TabIndex = 7;
            this._warningLabel.Text = "";
            //
            // _fileLabel
            //
            this._fileLabel.AutoEllipsis = true;
            this._fileLabel.Location = new System.Drawing.Point(12, 402);
            this._fileLabel.Name = "_fileLabel";
            this._fileLabel.Size = new System.Drawing.Size(690, 20);
            this._fileLabel.TabIndex = 3;
            this._fileLabel.Text = "저장: -";
            //
            // _folderButton
            //
            this._folderButton.Location = new System.Drawing.Point(710, 396);
            this._folderButton.Name = "_folderButton";
            this._folderButton.Size = new System.Drawing.Size(100, 28);
            this._folderButton.TabIndex = 4;
            this._folderButton.Text = "폴더 열기";
            this._folderButton.UseVisualStyleBackColor = true;
            this._folderButton.Click += new System.EventHandler(this.FolderButton_Click);
            //
            // _applyButton
            //
            this._applyButton.Location = new System.Drawing.Point(820, 396);
            this._applyButton.Name = "_applyButton";
            this._applyButton.Size = new System.Drawing.Size(100, 28);
            this._applyButton.TabIndex = 5;
            this._applyButton.Text = "적용";
            this._applyButton.UseVisualStyleBackColor = true;
            this._applyButton.Click += new System.EventHandler(this.ApplyButton_Click);
            //
            // _closeButton
            //
            this._closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this._closeButton.Location = new System.Drawing.Point(928, 396);
            this._closeButton.Name = "_closeButton";
            this._closeButton.Size = new System.Drawing.Size(100, 28);
            this._closeButton.TabIndex = 6;
            this._closeButton.Text = "닫기";
            this._closeButton.UseVisualStyleBackColor = true;
            //
            // F_ProbeTiltResult
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this._closeButton;
            this.ClientSize = new System.Drawing.Size(1040, 436);
            this.Controls.Add(this._verdictLabel);
            this.Controls.Add(this._grid);
            this.Controls.Add(this._noteLabel);
            this.Controls.Add(this._warningLabel);
            this.Controls.Add(this._fileLabel);
            this.Controls.Add(this._folderButton);
            this.Controls.Add(this._applyButton);
            this.Controls.Add(this._closeButton);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "F_ProbeTiltResult";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "프로브 기울기 측정 결과";
            ((System.ComponentModel.ISupportInitialize)(this._grid)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label _verdictLabel;
        private System.Windows.Forms.DataGridView _grid;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colOld;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colNew;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colSpread;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colRms;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colChangeBefore;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colChangeOld;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colChangeNew;
        private System.Windows.Forms.DataGridViewTextBoxColumn _colVerdict;
        private System.Windows.Forms.Label _noteLabel;
        private System.Windows.Forms.Label _warningLabel;
        private System.Windows.Forms.Label _fileLabel;
        private System.Windows.Forms.Button _folderButton;
        private System.Windows.Forms.Button _applyButton;
        private System.Windows.Forms.Button _closeButton;
    }
}
