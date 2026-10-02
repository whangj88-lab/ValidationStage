namespace ValidationStage
{
    partial class F_CoordSystem
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
            this._csTree = new System.Windows.Forms.TreeView();
            this._propsGroup = new System.Windows.Forms.GroupBox();
            this._nameLabel = new System.Windows.Forms.Label();
            this._nameBox = new System.Windows.Forms.TextBox();
            this._typeLabel = new System.Windows.Forms.Label();
            this._typeCombo = new System.Windows.Forms.ComboBox();
            this._positionLabel = new System.Windows.Forms.Label();
            this._xLabel = new System.Windows.Forms.Label();
            this._xBox = new System.Windows.Forms.TextBox();
            this._yLabel = new System.Windows.Forms.Label();
            this._yBox = new System.Windows.Forms.TextBox();
            this._zLabel = new System.Windows.Forms.Label();
            this._zBox = new System.Windows.Forms.TextBox();
            this._uLabel = new System.Windows.Forms.Label();
            this._uBox = new System.Windows.Forms.TextBox();
            this._vLabel = new System.Windows.Forms.Label();
            this._vBox = new System.Windows.Forms.TextBox();
            this._wLabel = new System.Windows.Forms.Label();
            this._wBox = new System.Windows.Forms.TextBox();
            this._setButton = new System.Windows.Forms.Button();
            this._expandButton = new System.Windows.Forms.Button();
            this._extraBox = new System.Windows.Forms.TextBox();
            this._addButton = new System.Windows.Forms.Button();
            this._deleteButton = new System.Windows.Forms.Button();
            this._activateButton = new System.Windows.Forms.Button();
            this._refreshButton = new System.Windows.Forms.Button();
            this._saveButton = new System.Windows.Forms.Button();
            this._saveMenuButton = new System.Windows.Forms.Button();
            this._statusBox = new System.Windows.Forms.TextBox();
            this._clearButton = new System.Windows.Forms.Button();
            this._toolTip = new System.Windows.Forms.ToolTip(this.components);
            this._propsGroup.SuspendLayout();
            this.SuspendLayout();
            //
            // _csTree
            //
            this._csTree.HideSelection = false;
            this._csTree.Location = new System.Drawing.Point(8, 10);
            this._csTree.Name = "_csTree";
            this._csTree.Size = new System.Drawing.Size(272, 334);
            this._csTree.TabIndex = 0;
            this._csTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.CsTree_AfterSelect);
            //
            // _propsGroup
            //
            this._propsGroup.Controls.Add(this._nameLabel);
            this._propsGroup.Controls.Add(this._nameBox);
            this._propsGroup.Controls.Add(this._typeLabel);
            this._propsGroup.Controls.Add(this._typeCombo);
            this._propsGroup.Controls.Add(this._positionLabel);
            this._propsGroup.Controls.Add(this._xLabel);
            this._propsGroup.Controls.Add(this._xBox);
            this._propsGroup.Controls.Add(this._yLabel);
            this._propsGroup.Controls.Add(this._yBox);
            this._propsGroup.Controls.Add(this._zLabel);
            this._propsGroup.Controls.Add(this._zBox);
            this._propsGroup.Controls.Add(this._uLabel);
            this._propsGroup.Controls.Add(this._uBox);
            this._propsGroup.Controls.Add(this._vLabel);
            this._propsGroup.Controls.Add(this._vBox);
            this._propsGroup.Controls.Add(this._wLabel);
            this._propsGroup.Controls.Add(this._wBox);
            this._propsGroup.Controls.Add(this._setButton);
            this._propsGroup.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this._propsGroup.Location = new System.Drawing.Point(286, 4);
            this._propsGroup.Name = "_propsGroup";
            this._propsGroup.Size = new System.Drawing.Size(138, 340);
            this._propsGroup.TabIndex = 1;
            this._propsGroup.TabStop = false;
            this._propsGroup.Text = "CS Properties";
            //
            // _nameLabel
            //
            this._nameLabel.AutoSize = true;
            this._nameLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._nameLabel.ForeColor = System.Drawing.Color.DimGray;
            this._nameLabel.Location = new System.Drawing.Point(6, 20);
            this._nameLabel.Name = "_nameLabel";
            this._nameLabel.Size = new System.Drawing.Size(42, 15);
            this._nameLabel.TabIndex = 0;
            this._nameLabel.Text = "Name:";
            //
            // _nameBox
            //
            this._nameBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._nameBox.Location = new System.Drawing.Point(8, 38);
            this._nameBox.Name = "_nameBox";
            this._nameBox.ReadOnly = true;
            this._nameBox.Size = new System.Drawing.Size(122, 23);
            this._nameBox.TabIndex = 1;
            //
            // _typeLabel
            //
            this._typeLabel.AutoSize = true;
            this._typeLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._typeLabel.ForeColor = System.Drawing.Color.DimGray;
            this._typeLabel.Location = new System.Drawing.Point(6, 64);
            this._typeLabel.Name = "_typeLabel";
            this._typeLabel.Size = new System.Drawing.Size(34, 15);
            this._typeLabel.TabIndex = 2;
            this._typeLabel.Text = "Type:";
            //
            // _typeCombo
            //
            this._typeCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this._typeCombo.Enabled = false;
            this._typeCombo.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._typeCombo.FormattingEnabled = true;
            this._typeCombo.Location = new System.Drawing.Point(8, 82);
            this._typeCombo.Name = "_typeCombo";
            this._typeCombo.Size = new System.Drawing.Size(122, 23);
            this._typeCombo.TabIndex = 3;
            //
            // _positionLabel
            //
            this._positionLabel.AutoSize = true;
            this._positionLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._positionLabel.Location = new System.Drawing.Point(62, 110);
            this._positionLabel.Name = "_positionLabel";
            this._positionLabel.Size = new System.Drawing.Size(53, 15);
            this._positionLabel.TabIndex = 4;
            this._positionLabel.Text = "Position:";
            //
            // _xLabel
            //
            this._xLabel.AutoSize = true;
            this._xLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._xLabel.Location = new System.Drawing.Point(6, 133);
            this._xLabel.Name = "_xLabel";
            this._xLabel.Size = new System.Drawing.Size(46, 15);
            this._xLabel.TabIndex = 5;
            this._xLabel.Text = "X [mm]:";
            //
            // _xBox
            //
            this._xBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._xBox.Location = new System.Drawing.Point(64, 130);
            this._xBox.Name = "_xBox";
            this._xBox.ReadOnly = true;
            this._xBox.Size = new System.Drawing.Size(66, 23);
            this._xBox.TabIndex = 6;
            //
            // _yLabel
            //
            this._yLabel.AutoSize = true;
            this._yLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._yLabel.Location = new System.Drawing.Point(6, 161);
            this._yLabel.Name = "_yLabel";
            this._yLabel.Size = new System.Drawing.Size(46, 15);
            this._yLabel.TabIndex = 7;
            this._yLabel.Text = "Y [mm]:";
            //
            // _yBox
            //
            this._yBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._yBox.Location = new System.Drawing.Point(64, 158);
            this._yBox.Name = "_yBox";
            this._yBox.ReadOnly = true;
            this._yBox.Size = new System.Drawing.Size(66, 23);
            this._yBox.TabIndex = 8;
            //
            // _zLabel
            //
            this._zLabel.AutoSize = true;
            this._zLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._zLabel.Location = new System.Drawing.Point(6, 189);
            this._zLabel.Name = "_zLabel";
            this._zLabel.Size = new System.Drawing.Size(46, 15);
            this._zLabel.TabIndex = 9;
            this._zLabel.Text = "Z [mm]:";
            //
            // _zBox
            //
            this._zBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._zBox.Location = new System.Drawing.Point(64, 186);
            this._zBox.Name = "_zBox";
            this._zBox.ReadOnly = true;
            this._zBox.Size = new System.Drawing.Size(66, 23);
            this._zBox.TabIndex = 10;
            //
            // _uLabel
            //
            this._uLabel.AutoSize = true;
            this._uLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._uLabel.Location = new System.Drawing.Point(6, 217);
            this._uLabel.Name = "_uLabel";
            this._uLabel.Size = new System.Drawing.Size(45, 15);
            this._uLabel.TabIndex = 11;
            this._uLabel.Text = "U [deg]:";
            //
            // _uBox
            //
            this._uBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._uBox.Location = new System.Drawing.Point(64, 214);
            this._uBox.Name = "_uBox";
            this._uBox.ReadOnly = true;
            this._uBox.Size = new System.Drawing.Size(66, 23);
            this._uBox.TabIndex = 12;
            //
            // _vLabel
            //
            this._vLabel.AutoSize = true;
            this._vLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._vLabel.Location = new System.Drawing.Point(6, 245);
            this._vLabel.Name = "_vLabel";
            this._vLabel.Size = new System.Drawing.Size(45, 15);
            this._vLabel.TabIndex = 13;
            this._vLabel.Text = "V [deg]:";
            //
            // _vBox
            //
            this._vBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._vBox.Location = new System.Drawing.Point(64, 242);
            this._vBox.Name = "_vBox";
            this._vBox.ReadOnly = true;
            this._vBox.Size = new System.Drawing.Size(66, 23);
            this._vBox.TabIndex = 14;
            //
            // _wLabel
            //
            this._wLabel.AutoSize = true;
            this._wLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._wLabel.Location = new System.Drawing.Point(6, 273);
            this._wLabel.Name = "_wLabel";
            this._wLabel.Size = new System.Drawing.Size(48, 15);
            this._wLabel.TabIndex = 15;
            this._wLabel.Text = "W [deg]:";
            //
            // _wBox
            //
            this._wBox.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._wBox.Location = new System.Drawing.Point(64, 270);
            this._wBox.Name = "_wBox";
            this._wBox.ReadOnly = true;
            this._wBox.Size = new System.Drawing.Size(66, 23);
            this._wBox.TabIndex = 16;
            //
            // _setButton
            //
            this._setButton.Enabled = false;
            this._setButton.Font = new System.Drawing.Font("Segoe UI", 9F);
            this._setButton.Location = new System.Drawing.Point(8, 302);
            this._setButton.Name = "_setButton";
            this._setButton.Size = new System.Drawing.Size(122, 27);
            this._setButton.TabIndex = 17;
            this._setButton.Text = "Set Coord. Sys.";
            this._setButton.UseVisualStyleBackColor = true;
            this._setButton.Click += new System.EventHandler(this.SetButton_Click);
            //
            // _expandButton
            //
            this._expandButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._expandButton.Location = new System.Drawing.Point(428, 10);
            this._expandButton.Name = "_expandButton";
            this._expandButton.Size = new System.Drawing.Size(18, 334);
            this._expandButton.TabIndex = 2;
            this._expandButton.Text = ">";
            this._expandButton.UseVisualStyleBackColor = true;
            this._expandButton.Click += new System.EventHandler(this.ExpandButton_Click);
            //
            // _extraBox
            //
            this._extraBox.Font = new System.Drawing.Font("Consolas", 8.25F);
            this._extraBox.Location = new System.Drawing.Point(452, 10);
            this._extraBox.Multiline = true;
            this._extraBox.Name = "_extraBox";
            this._extraBox.ReadOnly = true;
            this._extraBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this._extraBox.Size = new System.Drawing.Size(340, 334);
            this._extraBox.TabIndex = 3;
            this._extraBox.Visible = false;
            this._extraBox.WordWrap = false;
            //
            // _addButton
            //
            this._addButton.Font = new System.Drawing.Font("Segoe MDL2 Assets", 12F, System.Drawing.FontStyle.Bold);
            this._addButton.Location = new System.Drawing.Point(8, 352);
            this._addButton.Name = "_addButton";
            this._addButton.Size = new System.Drawing.Size(34, 28);
            this._addButton.TabIndex = 4;
            this._addButton.Text = "";
            this._addButton.UseVisualStyleBackColor = true;
            this._addButton.Click += new System.EventHandler(this.AddButton_Click);
            //
            // _deleteButton
            //
            this._deleteButton.Enabled = false;
            this._deleteButton.Font = new System.Drawing.Font("Segoe MDL2 Assets", 12F);
            this._deleteButton.Location = new System.Drawing.Point(48, 352);
            this._deleteButton.Name = "_deleteButton";
            this._deleteButton.Size = new System.Drawing.Size(34, 28);
            this._deleteButton.TabIndex = 5;
            this._deleteButton.Text = "";
            this._deleteButton.UseVisualStyleBackColor = true;
            this._deleteButton.Click += new System.EventHandler(this.DeleteButton_Click);
            //
            // _activateButton
            //
            this._activateButton.Location = new System.Drawing.Point(96, 352);
            this._activateButton.Name = "_activateButton";
            this._activateButton.Size = new System.Drawing.Size(84, 28);
            this._activateButton.TabIndex = 6;
            this._activateButton.Text = "Activate CS";
            this._activateButton.UseVisualStyleBackColor = true;
            this._activateButton.Click += new System.EventHandler(this.ActivateButton_Click);
            //
            // _refreshButton
            //
            this._refreshButton.Font = new System.Drawing.Font("Segoe MDL2 Assets", 11F);
            this._refreshButton.Location = new System.Drawing.Point(188, 352);
            this._refreshButton.Name = "_refreshButton";
            this._refreshButton.Size = new System.Drawing.Size(34, 28);
            this._refreshButton.TabIndex = 7;
            this._refreshButton.Text = "";
            this._refreshButton.UseVisualStyleBackColor = true;
            this._refreshButton.Click += new System.EventHandler(this.RefreshButton_Click);
            //
            // _saveButton
            //
            this._saveButton.Enabled = false;
            this._saveButton.Font = new System.Drawing.Font("Segoe UI", 8.25F);
            this._saveButton.Location = new System.Drawing.Point(228, 352);
            this._saveButton.Name = "_saveButton";
            this._saveButton.Size = new System.Drawing.Size(192, 28);
            this._saveButton.TabIndex = 8;
            this._saveButton.Text = "Save and Reset Coordinate System";
            this._saveButton.UseVisualStyleBackColor = true;
            //
            // _saveMenuButton
            //
            this._saveMenuButton.Enabled = false;
            this._saveMenuButton.Font = new System.Drawing.Font("Segoe MDL2 Assets", 10F);
            this._saveMenuButton.Location = new System.Drawing.Point(420, 352);
            this._saveMenuButton.Name = "_saveMenuButton";
            this._saveMenuButton.Size = new System.Drawing.Size(26, 28);
            this._saveMenuButton.TabIndex = 9;
            this._saveMenuButton.Text = "";
            this._saveMenuButton.UseVisualStyleBackColor = true;
            //
            // _statusBox
            //
            this._statusBox.Location = new System.Drawing.Point(8, 388);
            this._statusBox.Name = "_statusBox";
            this._statusBox.ReadOnly = true;
            this._statusBox.Size = new System.Drawing.Size(412, 23);
            this._statusBox.TabIndex = 10;
            //
            // _clearButton
            //
            this._clearButton.Font = new System.Drawing.Font("Segoe MDL2 Assets", 8F);
            this._clearButton.Location = new System.Drawing.Point(422, 388);
            this._clearButton.Name = "_clearButton";
            this._clearButton.Size = new System.Drawing.Size(24, 23);
            this._clearButton.TabIndex = 11;
            this._clearButton.Text = "";
            this._clearButton.UseVisualStyleBackColor = true;
            this._clearButton.Click += new System.EventHandler(this.ClearButton_Click);
            //
            // F_CoordSystem
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(454, 420);
            this.Controls.Add(this._csTree);
            this.Controls.Add(this._propsGroup);
            this.Controls.Add(this._expandButton);
            this.Controls.Add(this._extraBox);
            this.Controls.Add(this._addButton);
            this.Controls.Add(this._deleteButton);
            this.Controls.Add(this._activateButton);
            this.Controls.Add(this._refreshButton);
            this.Controls.Add(this._saveButton);
            this.Controls.Add(this._saveMenuButton);
            this.Controls.Add(this._statusBox);
            this.Controls.Add(this._clearButton);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "F_CoordSystem";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Manage Coordinate Systems";
            this.Load += new System.EventHandler(this.F_CoordSystem_Load);
            this._propsGroup.ResumeLayout(false);
            this._propsGroup.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TreeView _csTree;
        private System.Windows.Forms.GroupBox _propsGroup;
        private System.Windows.Forms.Label _nameLabel;
        private System.Windows.Forms.TextBox _nameBox;
        private System.Windows.Forms.Label _typeLabel;
        private System.Windows.Forms.ComboBox _typeCombo;
        private System.Windows.Forms.Label _positionLabel;
        private System.Windows.Forms.Label _xLabel;
        private System.Windows.Forms.TextBox _xBox;
        private System.Windows.Forms.Label _yLabel;
        private System.Windows.Forms.TextBox _yBox;
        private System.Windows.Forms.Label _zLabel;
        private System.Windows.Forms.TextBox _zBox;
        private System.Windows.Forms.Label _uLabel;
        private System.Windows.Forms.TextBox _uBox;
        private System.Windows.Forms.Label _vLabel;
        private System.Windows.Forms.TextBox _vBox;
        private System.Windows.Forms.Label _wLabel;
        private System.Windows.Forms.TextBox _wBox;
        private System.Windows.Forms.Button _setButton;
        private System.Windows.Forms.Button _expandButton;
        private System.Windows.Forms.TextBox _extraBox;
        private System.Windows.Forms.Button _addButton;
        private System.Windows.Forms.Button _deleteButton;
        private System.Windows.Forms.Button _activateButton;
        private System.Windows.Forms.Button _refreshButton;
        private System.Windows.Forms.Button _saveButton;
        private System.Windows.Forms.Button _saveMenuButton;
        private System.Windows.Forms.TextBox _statusBox;
        private System.Windows.Forms.Button _clearButton;
        private System.Windows.Forms.ToolTip _toolTip;
    }
}
