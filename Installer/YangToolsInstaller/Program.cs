using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YangToolsInstaller;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public class MainForm : Form
{
    private readonly Dictionary<string, CheckBox> _versionChecks = new();
    private Button _btnInstall, _btnUninstall;
    private Label _lblStatus;
    private ProgressBar _progress;
    private readonly string _userAddinsRoot;

    private static readonly string[] AllVersions = { "2021", "2022", "2023", "2024", "2025", "2026", "2027" };

    public MainForm()
    {
        _userAddinsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins");

        this.Text = "YangTools Revit 插件安装器 v2.0";
        this.Size = new Size(420, 420);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.Font = new Font("Segoe UI", 9);

        int y = 15;
        var title = new Label { Text = "YangTools Revit 插件", Location = new Point(20, y), AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold) };
        this.Controls.Add(title); y += 30;

        var subtitle = new Label { Text = "无需管理员权限，安装到当前用户目录", Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
        this.Controls.Add(subtitle); y += 28;

        var groupBox = new GroupBox { Text = "选择 Revit 版本", Location = new Point(20, y), Size = new Size(365, 170) };
        int cx = 20, cy = 20;
        foreach (var ver in AllVersions)
        {
            string addinPath = Path.Combine(_userAddinsRoot, ver, "YangTools.Revit.addin");
            bool installed = File.Exists(addinPath);
            var chk = new CheckBox { Text = $"Revit {ver}{(installed ? " ✅" : "")}", Location = new Point(cx, cy), AutoSize = true, Checked = installed, Tag = ver };
            groupBox.Controls.Add(chk);
            _versionChecks[ver] = chk;
            cx += 110;
            if (cx >= 300) { cx = 20; cy += 25; }
        }
        this.Controls.Add(groupBox); y += 185;

        _btnInstall = new Button { Text = "一键安装", Location = new Point(20, y), Size = new Size(110, 36), BackColor = Color.FromArgb(0, 122, 204), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnInstall.Click += BtnInstall_Click; this.Controls.Add(_btnInstall);

        _btnUninstall = new Button { Text = "一键卸载", Location = new Point(140, y), Size = new Size(110, 36), BackColor = Color.FromArgb(180, 60, 60), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
        _btnUninstall.Click += BtnUninstall_Click; this.Controls.Add(_btnUninstall);
        y += 50;

        _progress = new ProgressBar { Location = new Point(20, y), Size = new Size(365, 10), Visible = false, Style = ProgressBarStyle.Marquee };
        this.Controls.Add(_progress); y += 20;

        _lblStatus = new Label { Text = "准备就绪", Location = new Point(20, y), AutoSize = true, ForeColor = Color.Gray };
        this.Controls.Add(_lblStatus);
    }

    private void SetBusy(bool busy, string msg)
    {
        _btnInstall.Enabled = !busy; _btnUninstall.Enabled = !busy;
        _progress.Visible = busy; _lblStatus.Text = msg; _lblStatus.ForeColor = Color.Gray;
        Application.DoEvents();
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        var selected = _versionChecks.Where(c => c.Value.Checked).Select(c => c.Key).ToList();
        if (!selected.Any()) { MessageBox.Show("请至少勾选一个版本！", "提示"); return; }

        SetBusy(true, "正在提取部署包...");
        try
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "YangTools_Deploy_" + Guid.NewGuid().ToString("N")[..8]);
            await Task.Run(() =>
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("deploy.zip");
                if (stream == null) throw new Exception("找不到嵌入的部署包。");
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                archive.ExtractToDirectory(tempDir, true);
            });

            foreach (var ver in selected)
            {
                _lblStatus.Text = $"正在安装 Revit {ver}..."; Application.DoEvents();
                await Task.Run(() => InstallVersion(ver, tempDir));
            }

            try { Directory.Delete(tempDir, true); } catch { }
            _lblStatus.Text = "✅ 安装完成！请重启 Revit。"; _lblStatus.ForeColor = Color.Green;
            MessageBox.Show($"已安装到 Revit {string.Join("、", selected)}。\n\n请重启 Revit 以加载插件。", "安装成功");
            RefreshStatus();
        }
        catch (Exception ex) { ShowError("安装失败", ex); }
        finally { SetBusy(false, ""); }
    }

    private async void BtnUninstall_Click(object? sender, EventArgs e)
    {
        var selected = _versionChecks.Where(c => c.Value.Checked).Select(c => c.Key).ToList();
        if (!selected.Any()) { MessageBox.Show("请至少勾选一个版本！", "提示"); return; }
        if (MessageBox.Show($"确认卸载 Revit {string.Join("、", selected)} 的插件？", "确认", MessageBoxButtons.YesNo) != DialogResult.Yes) return;

        SetBusy(true, "正在卸载...");
        try
        {
            foreach (var ver in selected) { _lblStatus.Text = $"正在卸载 Revit {ver}..."; Application.DoEvents(); await Task.Run(() => UninstallVersion(ver)); }
            _lblStatus.Text = "✅ 卸载完成！"; _lblStatus.ForeColor = Color.Green;
            RefreshStatus();
        }
        catch (Exception ex) { ShowError("卸载失败", ex); }
        finally { SetBusy(false, ""); }
    }

    private void InstallVersion(string version, string tempDir)
    {
        string srcDir = Path.Combine(tempDir, version);
        if (!Directory.Exists(srcDir)) throw new Exception($"部署包中缺少 Revit {version} 的文件。");

        string destAddins = Path.Combine(_userAddinsRoot, version);
        string destYang = Path.Combine(destAddins, "YangTools");
        if (Directory.Exists(destYang)) Directory.Delete(destYang, true);
        Directory.CreateDirectory(destYang);

        string addinSrc = Path.Combine(srcDir, "YangTools.Revit.addin");
        if (File.Exists(addinSrc)) File.Copy(addinSrc, Path.Combine(destAddins, "YangTools.Revit.addin"), true);

        string yangSrc = Path.Combine(srcDir, "YangTools");
        if (Directory.Exists(yangSrc)) CopyDir(yangSrc, destYang);
    }

    private void UninstallVersion(string version)
    {
        string destAddins = Path.Combine(_userAddinsRoot, version);
        string pluginDir = Path.Combine(destAddins, "YangTools");
        string addinFile = Path.Combine(destAddins, "YangTools.Revit.addin");
        if (Directory.Exists(pluginDir)) Directory.Delete(pluginDir, true);
        if (File.Exists(addinFile)) File.Delete(addinFile);
    }

    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
    }

    private void RefreshStatus()
    {
        foreach (var (ver, chk) in _versionChecks)
        {
            bool installed = File.Exists(Path.Combine(_userAddinsRoot, ver, "YangTools.Revit.addin"));
            chk.Text = $"Revit {ver}{(installed ? " ✅" : "")}";
            chk.Checked = installed;
        }
    }

    private void ShowError(string title, Exception ex) { _lblStatus.Text = title; _lblStatus.ForeColor = Color.Red; MessageBox.Show($"{title}：\n{ex.Message}", "错误"); }
}
