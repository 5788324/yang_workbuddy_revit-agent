using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using Microsoft.Win32;

namespace YangTools.Revit.UI
{
    public partial class EntityGeneratorWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;

        public EntityGeneratorState State { get; private set; }
        public bool IsExecute { get; private set; } = false;
        public string ActionToPerform { get; private set; } = null;

        public EntityGeneratorWindow(UIApplication uiapp, EntityGeneratorState state)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            State = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = UserSettings.Load();
                string templatePath = State.TemplatePath;
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    templatePath = settings.EntityGeneratorTemplatePath;
                }
                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    string templateDir = _uiapp.Application.FamilyTemplatePath;
                    if (string.IsNullOrEmpty(templateDir))
                    {
                        string version = _uiapp.Application.VersionNumber;
                        templateDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            "Autodesk", $"RVT {version}", "Family Templates", "Chinese");
                    }
                    templatePath = Path.Combine(templateDir, "公制常规模型.rft");
                    if (!File.Exists(templatePath))
                        templatePath = Path.Combine(templateDir, "Generic Model.rft");
                }
                if (File.Exists(templatePath))
                    TxtTemplatePath.Text = templatePath;

                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                int levelIdx = 0;
                for (int i = 0; i < levels.Count; i++)
                {
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = levels[i].Name,
                        Tag = levels[i]
                    };
                    CmbLevel.Items.Add(item);
                    if (State.TargetLevelId != ElementId.InvalidElementId && levels[i].Id == State.TargetLevelId)
                        levelIdx = i;
                }
                if (CmbLevel.Items.Count > 0)
                    CmbLevel.SelectedIndex = levelIdx;

                var categories = new Dictionary<string, BuiltInCategory>();
                foreach (Category cat in _doc.Settings.Categories)
                {
                    if (cat != null && cat.CategoryType == CategoryType.Model && cat.AllowsBoundParameters && cat.CanAddSubcategory)
                    {
#if REVIT2024_OR_GREATER
                        long idVal = cat.Id.Value;
#else
                        int idVal = cat.Id.IntegerValue;
#endif
                        if (Enum.IsDefined(typeof(BuiltInCategory), idVal) && !categories.ContainsKey(cat.Name))
                            categories.Add(cat.Name, (BuiltInCategory)idVal);
                    }
                }

                var sortedCategories = categories.OrderBy(c => c.Key).ToList();
                int catIdx = 0;
                for (int i = 0; i < sortedCategories.Count; i++)
                {
                    var cat = sortedCategories[i];
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = cat.Key,
                        Tag = cat.Value
                    };
                    CmbCategory.Items.Add(item);
                    if (cat.Value == State.TargetCategory) catIdx = i;
                }
                if (CmbCategory.Items.Count > 0)
                    CmbCategory.SelectedIndex = catIdx;

                TxtFamilyName.Text = State.FamilyName;
                TxtMaterialName.Text = State.MaterialName;
                ChkIsVoid.IsChecked = State.IsVoid;
                ChkCutWithVoids.IsChecked = State.CutWithVoids;
                UpdateProfileLabel();
            }
            catch (Exception ex)
            {
                TaskDialog.Show("初始化错误", $"初始化窗口时发生错误：{ex.Message}");
            }
        }

        private void UpdateProfileLabel()
        {
            if (State.Profiles.Count > 0)
            {
                var ids = string.Join(", ", State.Profiles.Select(p => p.Id.ToString()));
                TxtProfiles.Text = $"已选择 {State.Profiles.Count} 个轮廓 (IDs: {ids})";
                TxtProfiles.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3A, 0x78, 0x3A));
            }
            else
            {
                TxtProfiles.Text = "未选择轮廓";
                TxtProfiles.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0x30, 0x20));
            }
        }

        private void PickProfiles()
        {
            State.TemplatePath = TxtTemplatePath.Text;
            State.FamilyName = TxtFamilyName.Text;
            State.MaterialName = TxtMaterialName.Text;
            if (CmbCategory.SelectedItem is System.Windows.Controls.ComboBoxItem catItem && catItem.Tag is BuiltInCategory targetCat)
                State.TargetCategory = targetCat;
            State.IsVoid = ChkIsVoid.IsChecked == true;
            State.CutWithVoids = ChkCutWithVoids.IsChecked == true;
            if (CmbLevel.SelectedItem is System.Windows.Controls.ComboBoxItem levelItem && levelItem.Tag is Level level)
                State.TargetLevelId = level.Id;

            ActionToPerform = "PickProfiles";
            this.Close();
        }

        private void Execute()
        {
            State.TemplatePath = TxtTemplatePath.Text;
            State.FamilyName = TxtFamilyName.Text;
            State.MaterialName = TxtMaterialName.Text;
            if (CmbCategory.SelectedItem is System.Windows.Controls.ComboBoxItem catItem && catItem.Tag is BuiltInCategory targetCat)
                State.TargetCategory = targetCat;
            State.IsVoid = ChkIsVoid.IsChecked == true;
            State.CutWithVoids = ChkCutWithVoids.IsChecked == true;
            if (CmbLevel.SelectedItem is System.Windows.Controls.ComboBoxItem levelItem && levelItem.Tag is Level level)
                State.TargetLevelId = level.Id;

            IsExecute = true;
            this.Close();
        }

        private void BtnSelectProfiles_Click(object sender, RoutedEventArgs e) => PickProfiles();
        private void BtnGenerate_Click(object sender, RoutedEventArgs e) => Execute();
        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "选择族模板"
            };
            if (dlg.ShowDialog() == true)
                TxtTemplatePath.Text = dlg.FileName;
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }
    }
}
