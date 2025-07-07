using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileCompressorApp
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if we have command line arguments for context menu operations
            if (args.Length >= 2)
            {
                string operation = args[0];
                string filePath = args[1];
                
                // Validate the file/folder exists
                if ((operation.ToLower() == "/compressfolder" || operation.ToLower() == "-compressfolder"))
                {
                    if (!Directory.Exists(filePath))
                    {
                        MessageBox.Show("المجلد غير موجود: " + filePath, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show("الملف غير موجود: " + filePath, "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                
                // Open GUI with context menu operation
                Application.Run(new Form1(operation, filePath));
                return;
            }

            // Normal GUI mode
            Application.Run(new Form1());
        }

    }
}