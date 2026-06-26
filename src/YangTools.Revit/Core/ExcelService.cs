using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class ExcelService
    {
        public static string ShowSaveDialog(string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel File (*.xlsx)|*.xlsx",
                FileName = defaultFileName
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static string ShowOpenDialog()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel File (*.xlsx)|*.xlsx"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public static bool ExportToExcel(string filePath, List<Dictionary<string, object>> data, string successMessage = "导出成功！(Export Successful)")
        {
            try
            {
                MiniExcelLibs.MiniExcel.SaveAs(filePath, data);
                TaskDialog.Show("提示", successMessage);
                return true;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("错误", "导出失败: " + ex.Message);
                return false;
            }
        }

        public static IEnumerable<IDictionary<string, object>> ReadExcel(string filePath)
        {
            return MiniExcelLibs.MiniExcel.Query(filePath, useHeaderRow: true).Cast<IDictionary<string, object>>();
        }
    }
}
