using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace YangTools.Revit.UI
{
    public partial class RevisionsWindow : Window
    {
        public List<RevisionItemViewModel> RevisionItems { get; set; } = new List<RevisionItemViewModel>();

        public RevisionsWindow(Document doc, ViewSheet sheet)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);

            var allRevisions = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Revisions)
                .WhereElementIsNotElementType()
                .Cast<Revision>()
                .OrderBy(r => r.SequenceNumber)
                .ToList();

            var currentRevIds = sheet.GetAllRevisionIds();

            foreach (var rev in allRevisions)
            {
                RevisionItems.Add(new RevisionItemViewModel
                {
                    Id = rev.Id,
                    DisplayName = $"{rev.SequenceNumber} - {rev.RevisionDate} - {rev.Description}",
                    IsSelected = currentRevIds.Contains(rev.Id)
                });
            }

            RevisionsList.ItemsSource = RevisionItems;
        }

        public ICollection<ElementId> SelectedRevisionIds
        {
            get
            {
                return RevisionItems.Where(r => r.IsSelected).Select(r => r.Id).ToList();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class RevisionItemViewModel
    {
        public ElementId Id { get; set; }
        public string DisplayName { get; set; }
        public bool IsSelected { get; set; }
    }
}
