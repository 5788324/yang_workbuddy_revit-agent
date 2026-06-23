using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MediaColor = System.Windows.Media.Color;

namespace YangTools.Revit.UI
{
    public class BooleanGeometryState
    {
        public List<FamilyInstance> MainInst = new List<FamilyInstance>();
        public List<FamilyInstance> UnionInsts = new List<FamilyInstance>();
        public List<FamilyInstance> CutInsts = new List<FamilyInstance>();
        public List<FamilyInstance> JoinInsts = new List<FamilyInstance>();
        public bool DeleteTargetInst = false;
        public bool DeleteTargetFam = false;
    }

    public partial class BooleanGeometryWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;
        
        public BooleanGeometryState State { get; private set; }
        public bool IsExecute { get; private set; } = false;
        public string ActionToPerform { get; private set; } = null;

        public BooleanGeometryWindow(UIApplication uiapp, BooleanGeometryState state)
        {
            InitializeComponent();
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            State = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ChkDeleteTargetInst.IsChecked = State.DeleteTargetInst;
            ChkDeleteTargetFam.IsChecked = State.DeleteTargetFam;
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            TxtMain.Text = State.MainInst.Count > 0 ? $"已选择: {State.MainInst[0].Name} (ID: {State.MainInst[0].Id})" : "未选择";
            TxtUnion.Text = State.UnionInsts.Count > 0 ? $"已选择: {State.UnionInsts.Count} 个图元 (IDs: {string.Join(", ", State.UnionInsts.Select(x => x.Id.ToString()))})" : "未选择";
            TxtCut.Text = State.CutInsts.Count > 0 ? $"已选择: {State.CutInsts.Count} 个图元 (IDs: {string.Join(", ", State.CutInsts.Select(x => x.Id.ToString()))})" : "未选择";
            TxtJoin.Text = State.JoinInsts.Count > 0 ? $"已选择: {State.JoinInsts.Count} 个图元 (IDs: {string.Join(", ", State.JoinInsts.Select(x => x.Id.ToString()))})" : "未选择";
        }

        private void TriggerPick(string action)
        {
            State.DeleteTargetInst = ChkDeleteTargetInst.IsChecked == true;
            State.DeleteTargetFam = ChkDeleteTargetFam.IsChecked == true;
            ActionToPerform = action;
            this.Close();
        }

        private void BtnSelectMain_Click(object sender, RoutedEventArgs e) => TriggerPick("PickMain");
        private void BtnSelectUnion_Click(object sender, RoutedEventArgs e) => TriggerPick("PickUnion");
        private void BtnSelectCut_Click(object sender, RoutedEventArgs e) => TriggerPick("PickCut");
        private void BtnSelectJoin_Click(object sender, RoutedEventArgs e) => TriggerPick("PickJoin");

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (State.MainInst.Count == 0)
            {
                MessageBox.Show("请先选择主体实例。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            State.DeleteTargetInst = ChkDeleteTargetInst.IsChecked == true;
            State.DeleteTargetFam = ChkDeleteTargetFam.IsChecked == true;
            
            IsExecute = true;
            this.Close();
        }
    }
}
