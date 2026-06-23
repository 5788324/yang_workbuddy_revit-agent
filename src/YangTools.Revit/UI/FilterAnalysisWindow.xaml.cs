using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class FilterAnalysisWindow : Window
    {
        private Document _doc;
        private ParameterFilterElement _filter;

        public class ElementItem
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
        }

        public FilterAnalysisWindow(Document doc, ParameterFilterElement filter)
        {
            InitializeComponent();
            ThemeHelper.ApplyToWindow(this);
            _doc = doc;
            _filter = filter;
            
            AnalyzeFilter();
        }

        private string GetFilterRulesString(ElementFilter filter, int indent = 0)
        {
            if (filter == null) return "无规则 / None";
            string prefix = new string(' ', indent * 4);
            if (filter is LogicalAndFilter andFilter)
            {
                var children = andFilter.GetFilters().Select(f => GetFilterRulesString(f, indent + 1));
                return prefix + "和 (所有规则必须为 true)\n" + string.Join("\n", children);
            }
            if (filter is LogicalOrFilter orFilter)
            {
                var children = orFilter.GetFilters().Select(f => GetFilterRulesString(f, indent + 1));
                return prefix + "或 (任何规则为 true)\n" + string.Join("\n", children);
            }
            if (filter is ElementParameterFilter paramFilter)
            {
                var rules = paramFilter.GetRules();
                var ruleStrings = new List<string>();
                foreach (var r in rules)
                {
                    string pName = $"参数ID: {r.GetRuleParameter().GetIdValue()}";
                    try
                    {
                        var paramId = r.GetRuleParameter();
                        if (paramId.GetIdValue() < 0)
                        {
                            var builtIn = (BuiltInParameter)(int)paramId.GetIdValue();
                            pName = LabelUtils.GetLabelFor(builtIn);
                        }
                        else
                        {
                            var param = _doc.GetElement(paramId) as ParameterElement;
                            if (param != null) pName = param.GetDefinition().Name;
                        }
                    } catch { }

                    string ruleDetails = "规则配置";
                    try 
                    {
                        if (r is FilterStringRule sr)
                        {
                            string eval = sr.GetEvaluator().GetType().Name.Replace("FilterString", "");
                            ruleDetails = $"{eval} '{sr.RuleString}'";
                        }
                        else if (r is FilterDoubleRule dr)
                        {
                            string eval = dr.GetEvaluator().GetType().Name.Replace("FilterNumeric", "");
                            ruleDetails = $"{eval} {dr.RuleValue}";
                        }
                        else if (r is FilterIntegerRule ir)
                        {
                            string eval = ir.GetEvaluator().GetType().Name.Replace("FilterNumeric", "");
                            ruleDetails = $"{eval} {ir.RuleValue}";
                        }
                        else if (r is FilterElementIdRule idr)
                        {
                            string eval = idr.GetEvaluator().GetType().Name.Replace("FilterNumeric", "");
                            
                            string valName = idr.RuleValue.GetIdValue().ToString();
                            try 
                            {
                                var ruleEl = _doc.GetElement(idr.RuleValue);
                                if (ruleEl != null) valName = ruleEl.Name;
                            } catch {}

                            ruleDetails = $"{eval} '{valName}'";
                        }
                    } catch { }

                    ruleStrings.Add($"{prefix}■ {pName} -> {ruleDetails}");
                }
                return string.Join("\n", ruleStrings);
            }
            return prefix + filter.GetType().Name;
        }

        private void AnalyzeFilter()
        {
            if (_filter == null) return;

            FilterNameText.Text = $"过滤器: {_filter.Name}";

            // A. Categories
            var catIds = _filter.GetCategories();
            var catNames = new List<string>();
            foreach (var catId in catIds)
            {
                var cat = Category.GetCategory(_doc, catId);
                if (cat != null)
                {
                    catNames.Add(cat.Name);
                }
            }
            catNames.Sort();
            CategoriesListBox.ItemsSource = catNames;

            // B. Elements
            var elementsList = new List<ElementItem>();
            if (catIds.Count > 0)
            {
                // Create a multi-category filter
                var catFilter = new ElementMulticategoryFilter(catIds);
                var elemFilter = _filter.GetElementFilter();
                
                var collector = new FilteredElementCollector(_doc).WhereElementIsNotElementType().WherePasses(catFilter);
                if (elemFilter != null)
                {
                    collector = collector.WherePasses(elemFilter);
                }

                foreach (var el in collector)
                {
                    elementsList.Add(new ElementItem
                    {
                        Id = el.Id.GetIdValue(),
                        Name = el.Name,
                        Category = el.Category?.Name ?? "未知"
                    });
                }
            }
            
            elementsList = elementsList.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
            ElementsDataGrid.ItemsSource = elementsList;

            // C. Views
            var viewsList = new List<string>();
            var allViews = new FilteredElementCollector(_doc).OfClass(typeof(View)).ToElements().Cast<View>();
            
            foreach (var view in allViews)
            {
                try
                {
                    if (view.AreGraphicsOverridesAllowed())
                    {
                        var filters = view.GetFilters();
                        if (filters.Contains(_filter.Id))
                        {
                            string prefix = view.IsTemplate ? "[视图样板]" : "[实体视图]";
                            viewsList.Add($"{prefix} {view.ViewType}: {view.Name}");
                        }
                    }
                }
                catch { }
            }
            
            viewsList.Sort();
            ViewsListBox.ItemsSource = viewsList;

            // D. Rules
            FilterRulesTextBox.Text = GetFilterRulesString(_filter.GetElementFilter());

            FilterSummaryText.Text = $"分析完成: 控制 {catNames.Count} 个类别, 匹配 {elementsList.Count} 个图元, 被 {viewsList.Count} 个视图使用。";
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
