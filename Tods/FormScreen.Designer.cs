namespace Tods
{
    partial class FormScreen
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mainSplitContainer = new System.Windows.Forms.SplitContainer();
            this.gamePanel = new Tods.DrawingPanel();
            this.statSplitContainer = new System.Windows.Forms.SplitContainer();
            this.minimapPanel = new Tods.DrawingPanel();
            this.statPanel = new Tods.DrawingPanel();
            this.gameTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).BeginInit();
            this.mainSplitContainer.Panel1.SuspendLayout();
            this.mainSplitContainer.Panel2.SuspendLayout();
            this.mainSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statSplitContainer)).BeginInit();
            this.statSplitContainer.Panel1.SuspendLayout();
            this.statSplitContainer.Panel2.SuspendLayout();
            this.statSplitContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainSplitContainer
            // 
            this.mainSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainSplitContainer.Location = new System.Drawing.Point(0, 0);
            this.mainSplitContainer.Name = "mainSplitContainer";
            // 
            // mainSplitContainer.Panel1
            // 
            this.mainSplitContainer.Panel1.Controls.Add(this.gamePanel);
            // 
            // mainSplitContainer.Panel2
            // 
            this.mainSplitContainer.Panel2.Controls.Add(this.statSplitContainer);
            this.mainSplitContainer.Size = new System.Drawing.Size(1450, 786);
            this.mainSplitContainer.SplitterDistance = 1421;
            this.mainSplitContainer.TabIndex = 0;
            this.mainSplitContainer.TabStop = false;
            // 
            // gamePanel
            // 
            this.gamePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gamePanel.Location = new System.Drawing.Point(0, 0);
            this.gamePanel.Name = "gamePanel";
            this.gamePanel.Size = new System.Drawing.Size(1421, 786);
            this.gamePanel.TabIndex = 0;
            this.gamePanel.Click += new System.EventHandler(this.gamePanel_Click);
            // 
            // statSplitContainer
            // 
            this.statSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statSplitContainer.Location = new System.Drawing.Point(0, 0);
            this.statSplitContainer.Name = "statSplitContainer";
            this.statSplitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // statSplitContainer.Panel1
            // 
            this.statSplitContainer.Panel1.Controls.Add(this.minimapPanel);
            // 
            // statSplitContainer.Panel2
            // 
            this.statSplitContainer.Panel2.Controls.Add(this.statPanel);
            this.statSplitContainer.Size = new System.Drawing.Size(25, 786);
            this.statSplitContainer.SplitterDistance = 295;
            this.statSplitContainer.TabIndex = 0;
            this.statSplitContainer.TabStop = false;
            // 
            // minimapPanel
            // 
            this.minimapPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.minimapPanel.Location = new System.Drawing.Point(0, 0);
            this.minimapPanel.Name = "minimapPanel";
            this.minimapPanel.Size = new System.Drawing.Size(25, 295);
            this.minimapPanel.TabIndex = 0;
            // 
            // statPanel
            // 
            this.statPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.statPanel.Location = new System.Drawing.Point(0, 0);
            this.statPanel.Name = "statPanel";
            this.statPanel.Size = new System.Drawing.Size(25, 487);
            this.statPanel.TabIndex = 0;
            // 
            // gameTimer
            // 
            this.gameTimer.Enabled = true;
            this.gameTimer.Interval = 16;
            this.gameTimer.Tick += new System.EventHandler(this.gameTimer_Tick);
            // 
            // FormScreen
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(1450, 786);
            this.Controls.Add(this.mainSplitContainer);
            this.Name = "FormScreen";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "The Day Of Sagittarius";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormScreen_FormClosed);
            this.Load += new System.EventHandler(this.FormScreen_Load);
            this.mainSplitContainer.Panel1.ResumeLayout(false);
            this.mainSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.mainSplitContainer)).EndInit();
            this.mainSplitContainer.ResumeLayout(false);
            this.statSplitContainer.Panel1.ResumeLayout(false);
            this.statSplitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.statSplitContainer)).EndInit();
            this.statSplitContainer.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer mainSplitContainer;
        private System.Windows.Forms.SplitContainer statSplitContainer;
        private System.Windows.Forms.Timer gameTimer;
        private DrawingPanel gamePanel;
        private DrawingPanel minimapPanel;
        private DrawingPanel statPanel;
    }
}

