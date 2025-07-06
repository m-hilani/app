namespace FileCompressorApp
{
    partial class Form1
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
            this.btnCompress = new System.Windows.Forms.Button();
            this.btnDecompress = new System.Windows.Forms.Button();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnPauseResume = new System.Windows.Forms.Button();
            this.radioHuffman = new System.Windows.Forms.RadioButton();
            this.radioFanoShannon = new System.Windows.Forms.RadioButton();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnCompare = new System.Windows.Forms.Button();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnMultiCompress = new System.Windows.Forms.Button();
            this.btnExtractSingle = new System.Windows.Forms.Button();
            this.btnCompressFolder = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCompress
            // 
            this.btnCompress.Location = new System.Drawing.Point(30, 30);
            this.btnCompress.Name = "btnCompress";
            this.btnCompress.Size = new System.Drawing.Size(150, 40);
            this.btnCompress.TabIndex = 0;
            this.btnCompress.Text = "ضغط ملف واحد";
            this.btnCompress.UseVisualStyleBackColor = true;
            this.btnCompress.Click += new System.EventHandler(this.btnCompress_Click);
            // 
            // btnDecompress
            // 
            this.btnDecompress.Location = new System.Drawing.Point(30, 80);
            this.btnDecompress.Name = "btnDecompress";
            this.btnDecompress.Size = new System.Drawing.Size(150, 40);
            this.btnDecompress.TabIndex = 1;
            this.btnDecompress.Text = "فك الضغط";
            this.btnDecompress.UseVisualStyleBackColor = true;
            this.btnDecompress.Click += new System.EventHandler(this.btnDecompress_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(30, 270);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(350, 30);
            this.progressBar1.TabIndex = 2;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(30, 250);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(32, 17);
            this.lblStatus.TabIndex = 3;
            this.lblStatus.Text = "جاهز";
            // 
            // btnPauseResume
            // 
            this.btnPauseResume.Enabled = false;
            this.btnPauseResume.Location = new System.Drawing.Point(230, 80);
            this.btnPauseResume.Name = "btnPauseResume";
            this.btnPauseResume.Size = new System.Drawing.Size(150, 40);
            this.btnPauseResume.TabIndex = 5;
            this.btnPauseResume.Text = "إيقاف مؤقت";
            this.btnPauseResume.UseVisualStyleBackColor = true;
            this.btnPauseResume.Click += new System.EventHandler(this.btnPauseResume_Click);
            // 
            // radioHuffman
            // 
            this.radioHuffman.AutoSize = true;
            this.radioHuffman.Checked = true;
            this.radioHuffman.Location = new System.Drawing.Point(20, 20);
            this.radioHuffman.Name = "radioHuffman";
            this.radioHuffman.Size = new System.Drawing.Size(60, 21);
            this.radioHuffman.TabIndex = 6;
            this.radioHuffman.TabStop = true;
            this.radioHuffman.Text = "هوفمان";
            this.radioHuffman.UseVisualStyleBackColor = true;
            this.radioHuffman.CheckedChanged += new System.EventHandler(this.radioHuffman_CheckedChanged);
            // 
            // radioFanoShannon
            // 
            this.radioFanoShannon.AutoSize = true;
            this.radioFanoShannon.Location = new System.Drawing.Point(20, 50);
            this.radioFanoShannon.Name = "radioFanoShannon";
            this.radioFanoShannon.Size = new System.Drawing.Size(77, 21);
            this.radioFanoShannon.TabIndex = 7;
            this.radioFanoShannon.Text = "فانو-شانون";
            this.radioFanoShannon.UseVisualStyleBackColor = true;
            this.radioFanoShannon.CheckedChanged += new System.EventHandler(this.radioFanoShannon_CheckedChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioHuffman);
            this.groupBox1.Controls.Add(this.radioFanoShannon);
            this.groupBox1.Location = new System.Drawing.Point(30, 320);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(350, 80);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "خوارزمية الضغط";
            // 
            // btnCompare
            // 
            this.btnCompare.Location = new System.Drawing.Point(230, 180);
            this.btnCompare.Name = "btnCompare";
            this.btnCompare.Size = new System.Drawing.Size(150, 40);
            this.btnCompare.TabIndex = 9;
            this.btnCompare.Text = "مقارنة الخوارزميات";
            this.btnCompare.UseVisualStyleBackColor = true;
            this.btnCompare.Click += new System.EventHandler(this.btnCompare_Click);
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(30, 200);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(150, 22);
            this.txtPassword.TabIndex = 10;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(30, 180);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(56, 17);
            this.label1.TabIndex = 11;
            this.label1.Text = "كلمة السر:";
            // 
            // btnMultiCompress
            // 
            this.btnMultiCompress.Location = new System.Drawing.Point(30, 130);
            this.btnMultiCompress.Name = "btnMultiCompress";
            this.btnMultiCompress.Size = new System.Drawing.Size(150, 40);
            this.btnMultiCompress.TabIndex = 12;
            this.btnMultiCompress.Text = "ضغط عدة ملفات";
            this.btnMultiCompress.UseVisualStyleBackColor = true;
            this.btnMultiCompress.Click += new System.EventHandler(this.btnMultiCompress_Click);
            // 
            // btnExtractSingle
            // 
            this.btnExtractSingle.Location = new System.Drawing.Point(230, 130);
            this.btnExtractSingle.Name = "btnExtractSingle";
            this.btnExtractSingle.Size = new System.Drawing.Size(150, 40);
            this.btnExtractSingle.TabIndex = 13;
            this.btnExtractSingle.Text = "استخراج ملف واحد";
            this.btnExtractSingle.UseVisualStyleBackColor = true;
            this.btnExtractSingle.Click += new System.EventHandler(this.btnExtractSingle_Click);
            // 
            // btnCompressFolder
            // 
            this.btnCompressFolder.Location = new System.Drawing.Point(230, 30);
            this.btnCompressFolder.Name = "btnCompressFolder";
            this.btnCompressFolder.Size = new System.Drawing.Size(150, 40);
            this.btnCompressFolder.TabIndex = 14;
            this.btnCompressFolder.Text = "ضغط مجلد";
            this.btnCompressFolder.UseVisualStyleBackColor = true;
            this.btnCompressFolder.Click += new System.EventHandler(this.btnCompressFolder_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 420);
            this.Controls.Add(this.btnExtractSingle);
            this.Controls.Add(this.btnMultiCompress);
            this.Controls.Add(this.btnCompressFolder);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.btnCompare);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnPauseResume);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar1);
            this.Controls.Add(this.btnDecompress);
            this.Controls.Add(this.btnCompress);
            this.Name = "Form1";
            this.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.Text = "تطبيق ضغط الملفات المتقدم";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        private System.Windows.Forms.Button btnCompress;
        private System.Windows.Forms.Button btnDecompress;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnPauseResume;
        private System.Windows.Forms.RadioButton radioHuffman;
        private System.Windows.Forms.RadioButton radioFanoShannon;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnCompare;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnMultiCompress;
        private System.Windows.Forms.Button btnExtractSingle;
        private System.Windows.Forms.Button btnCompressFolder;
    }
}