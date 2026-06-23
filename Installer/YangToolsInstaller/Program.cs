using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace YangToolsInstaller
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new InstallerForm());
        }
    }

    public class InstallerForm : Form
    {
        private CheckBox chk2024;
        private CheckBox chk2025;
        private CheckBox chk2027;
        private Button btnInstall;
        private Button btnUninstall;
        private Label lblStatus;

        public InstallerForm()
        {
            this.Text = "YangTools Revit 插件安装程序";
            this.Size = new Size(400, 280);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label title = new Label
            {
                Text = "选择需要安装或卸载的 Revit 版本：",
                Location = new Point(20, 20),
                AutoSize = true,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(title);

            chk2024 = new CheckBox
            {
                Text = "Revit 2024",
                Location = new Point(40, 60),
                AutoSize = true,
                Checked = true
            };
            this.Controls.Add(chk2024);

            chk2025 = new CheckBox
            {
                Text = "Revit 2025",
                Location = new Point(40, 90),
                AutoSize = true,
                Checked = true
            };
            this.Controls.Add(chk2025);

            chk2027 = new CheckBox
            {
                Text = "Revit 2027",
                Location = new Point(40, 120),
                AutoSize = true,
                Checked = true
            };
            this.Controls.Add(chk2027);

            btnInstall = new Button
            {
                Text = "一键安装",
                Location = new Point(40, 170),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnInstall.Click += BtnInstall_Click;
            this.Controls.Add(btnInstall);

            btnUninstall = new Button
            {
                Text = "完全卸载",
                Location = new Point(150, 170),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(204, 0, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnUninstall.Click += BtnUninstall_Click;
            this.Controls.Add(btnUninstall);

            lblStatus = new Label
            {
                Text = "准备就绪。",
                Location = new Point(20, 220),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            this.Controls.Add(lblStatus);
        }

        private void BtnInstall_Click(object? sender, EventArgs e)
        {
            try
            {
                bool i2024 = chk2024.Checked;
                bool i2025 = chk2025.Checked;
                bool i2027 = chk2027.Checked;

                if (!i2024 && !i2025 && !i2027)
                {
                    MessageBox.Show("请至少选择一个 Revit 版本！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (i2024) InstallForVersion("2024", "YangTools.Revit.net48.dll");
                if (i2025) InstallForVersion("2025", "YangTools.Revit.net8.dll");
                if (i2027) InstallForVersion("2027", "YangTools.Revit.net8.dll");

                lblStatus.Text = "安装成功！请重启 Revit。";
                lblStatus.ForeColor = Color.Green;
                MessageBox.Show("YangTools 插件安装成功！\n\n请重启 Revit 以加载插件。", "安装成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"安装失败：\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "安装失败。";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void BtnUninstall_Click(object? sender, EventArgs e)
        {
            try
            {
                bool u2024 = chk2024.Checked;
                bool u2025 = chk2025.Checked;
                bool u2027 = chk2027.Checked;

                if (!u2024 && !u2025 && !u2027)
                {
                    MessageBox.Show("请至少选择一个 Revit 版本！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (u2024) UninstallForVersion("2024");
                if (u2025) UninstallForVersion("2025");
                if (u2027) UninstallForVersion("2027");

                lblStatus.Text = "卸载成功！";
                lblStatus.ForeColor = Color.Green;
                MessageBox.Show("YangTools 插件已成功卸载。", "卸载成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"卸载失败：\n{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "卸载失败。";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void InstallForVersion(string version, string dllResourceName)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitAddins = Path.Combine(appData, "Autodesk", "Revit", "Addins", version);
            string pluginDir = Path.Combine(revitAddins, "YangTools");

            if (!Directory.Exists(revitAddins))
            {
                Directory.CreateDirectory(revitAddins);
            }

            if (!Directory.Exists(pluginDir))
            {
                Directory.CreateDirectory(pluginDir);
            }

            ExtractResource(dllResourceName, Path.Combine(pluginDir, "YangTools.Revit.dll"));
            ExtractResource("YangTools.Revit.addin", Path.Combine(revitAddins, "YangTools.Revit.addin"));
        }

        private void UninstallForVersion(string version)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string revitAddins = Path.Combine(appData, "Autodesk", "Revit", "Addins", version);
            string pluginDir = Path.Combine(revitAddins, "YangTools");
            string addinFile = Path.Combine(revitAddins, "YangTools.Revit.addin");

            if (Directory.Exists(pluginDir))
            {
                Directory.Delete(pluginDir, true);
            }

            if (File.Exists(addinFile))
            {
                File.Delete(addinFile);
            }
        }

        private void ExtractResource(string resourceName, string destinationPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new Exception($"找不到嵌入的资源: {resourceName}");

                using (FileStream fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }
    }
}