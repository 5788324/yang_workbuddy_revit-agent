using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Document = Autodesk.Revit.DB.Document;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands;

[Transaction(TransactionMode.Manual)]
[RibbonButton("模型修改区", "标高修改", "修改图元标高并保持原位置", "Icons/level_modifier_32.png", "Icons/level_modifier_16.png")]
public class LevelModifierCommand : IExternalCommand
{
	public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
	{
		try
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			if (activeUIDocument == null)
			{
				TaskDialog.Show("提示", "请先打开一个项目文档。");
				return Result.Cancelled;
			}
			Document document = activeUIDocument.Document;
			ICollection<ElementId> elementIds = activeUIDocument.Selection.GetElementIds();
			if (elementIds.Count == 0)
			{
				TaskDialog.Show("提示", "请先选择需要修改标高的图元。");
				return Result.Cancelled;
			}
			List<Level> list = (from Level l in new FilteredElementCollector(document).OfClass(typeof(Level))
				orderby l.Elevation
				select l).ToList();
			if (list.Count == 0)
			{
				TaskDialog.Show("错误", "项目中没有找到标高。");
				return Result.Failed;
			}
			LevelModifierWindow levelModifierWindow = new LevelModifierWindow(list);
			new System.Windows.Interop.WindowInteropHelper(levelModifierWindow).Owner = commandData.Application.MainWindowHandle;
			levelModifierWindow.ShowDialog();
			if (levelModifierWindow.IsOk && levelModifierWindow.SelectedLevel != null)
			{
				Level selectedLevel = levelModifierWindow.SelectedLevel;
				int num = 0;
				int num2 = 0;
				using (Transaction val = new Transaction(document, "修改标高保持原位"))
				{
					try
					{
						val.Start();
						foreach (ElementId item in elementIds)
						{
							Element element = document.GetElement(item);
							if (element != null)
							{
								if (TryChangeLevel(document, element, selectedLevel))
								{
									num++;
								}
								else
								{
									num2++;
								}
							}
						}
						TransactionHelper.ShowSuccessAndCommit(val, "成功", $"操作完成。\n成功: {num}\n失败或无需修改: {num2}", activeUIDocument);
						return Result.Succeeded;
					}
					catch (Exception ex)
					{
						if (val.GetStatus() == TransactionStatus.Started) val.RollBack();
						TaskDialog.Show("错误", $"操作失败，已撤销。\n错误信息: {ex.Message}");
						message = ex.Message;
						return Result.Failed;
					}
				}
			}
			return Result.Cancelled;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			TaskDialog.Show("错误", $"发生异常。\n错误信息: {ex.Message}");
			return Result.Failed;
		}
	}

	private bool TryChangeLevel(Document doc, Element elem, Level targetLevel)
	{
		Parameter levelParameter = GetLevelParameter(elem);
		if (levelParameter != null && !((APIObject)levelParameter).IsReadOnly)
		{
			Parameter offsetParameter = GetOffsetParameter(elem, levelParameter);
			if (offsetParameter != null && !((APIObject)offsetParameter).IsReadOnly)
			{
				ElementId val = levelParameter.AsElementId();
				if (val == ((Element)targetLevel).Id)
				{
					return true;
				}
				Element element = doc.GetElement(val);
				Level val2 = (Level)(object)((element is Level) ? element : null);
				double num = ((val2 != null) ? val2.Elevation : 0.0);
				double num2 = offsetParameter.AsDouble();
				double num3 = num + num2 - targetLevel.Elevation;
				levelParameter.Set(((Element)targetLevel).Id);
				offsetParameter.Set(num3);
				return true;
			}
		}
		FamilyInstance val3 = (FamilyInstance)(object)((elem is FamilyInstance) ? elem : null);
		if (val3 != null)
		{
			Location location = ((Element)val3).Location;
			LocationPoint val4 = (LocationPoint)(object)((location is LocationPoint) ? location : null);
			if (val4 != null)
			{
				XYZ point = val4.Point;
				FamilySymbol symbol = val3.Symbol;
				if (!symbol.IsActive)
				{
					symbol.Activate();
				}
				try
				{
					FamilyInstance val5 = ((ItemFactoryBase)doc.Create).NewFamilyInstance(point, symbol, targetLevel, (StructuralType)0);
					if (Math.Abs(val4.Rotation) > 0.0001)
					{
						Line val6 = Line.CreateBound(point, point + XYZ.BasisZ);
						ElementTransformUtils.RotateElement(doc, ((Element)val5).Id, val6, val4.Rotation);
					}
					if (val3.Mirrored)
					{
						if (val3.CanFlipHand && val3.HandFlipped)
						{
							val5.flipHand();
						}
						if (val3.CanFlipFacing && val3.FacingFlipped)
						{
							val5.flipFacing();
						}
					}
					CopyInstanceParameters((Element)(object)val3, (Element)(object)val5, levelParameter, GetOffsetParameter(elem, levelParameter));
					doc.Delete(elem.Id);
					return true;
				}
				catch
				{
					return false;
				}
			}
		}
		return false;
	}

	private void CopyInstanceParameters(Element source, Element target, Parameter levelParam, Parameter offsetParam)
	{
		foreach (Parameter parameter in source.Parameters)
		{
			Parameter val = parameter;
			if ((levelParam != null && val.Id == levelParam.Id) || (offsetParam != null && val.Id == offsetParam.Id) || ((APIObject)val).IsReadOnly || val.Definition == null)
			{
				continue;
			}
			Parameter val2 = target.get_Parameter(val.Definition);
			if (val2 != null && !((APIObject)val2).IsReadOnly)
			{
				StorageType storageType = val.StorageType;
				switch ((int)storageType - 1)
				{
				case 1:
					val2.Set(val.AsDouble());
					break;
				case 0:
					val2.Set(val.AsInteger());
					break;
				case 2:
					val2.Set(val.AsString());
					break;
				case 3:
					val2.Set(val.AsElementId());
					break;
				}
			}
		}
	}

	private Parameter GetLevelParameter(Element elem)
	{
		BuiltInParameter[] array2 = new BuiltInParameter[] {
            BuiltInParameter.FAMILY_LEVEL_PARAM,
            BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM,
            BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM,
            BuiltInParameter.LEVEL_PARAM,
            BuiltInParameter.ROOM_LEVEL_ID,
            BuiltInParameter.RBS_START_LEVEL_PARAM,
            BuiltInParameter.SCHEDULE_LEVEL_PARAM,
            BuiltInParameter.WALL_BASE_CONSTRAINT
        };
		foreach (BuiltInParameter val in array2)
		{
			Parameter val2 = elem.get_Parameter(val);
			if (val2 != null && (int)val2.StorageType == 4)
			{
				return val2;
			}
		}
		foreach (Parameter parameter in elem.Parameters)
		{
			Parameter val3 = parameter;
			if ((int)val3.StorageType != 4)
			{
				continue;
			}
			string name = val3.Definition.Name;
			if (name.Contains("Level") || name.Contains("标高") || name.Contains("Base Constraint") || name.Contains("底部约束") || name.Contains("Reference Level"))
			{
				ElementId val4 = val3.AsElementId();
				if (!(val4 != ElementId.InvalidElementId))
				{
					return val3;
				}
				if (elem.Document.GetElement(val4) is Level)
				{
					return val3;
				}
			}
		}
		return null;
	}

	private Parameter GetOffsetParameter(Element elem, Parameter levelParam)
	{
		if (levelParam != null && levelParam.Id.GetIdValue() < 0)
		{
			BuiltInParameter val = (BuiltInParameter)(int)levelParam.Id.GetIdValue();
			BuiltInParameter? val2 = null;
			if ((long)val <= -1001708L)
			{
				if ((long)val != -1114000)
				{
					if ((long)val != -1007200)
					{
						if ((long)val == -1001708)
						{
							val2 = (BuiltInParameter)(-1001701);
						}
					}
					else
					{
						val2 = (BuiltInParameter)(-1007218);
					}
				}
				else
				{
					val2 = (BuiltInParameter)(-1114132);
				}
			}
			else if ((long)val != -1001383 && (long)val != -1001352)
			{
				if ((long)val == -1001107)
				{
					val2 = (BuiltInParameter)(-1001108);
				}
			}
			else
			{
				val2 = (BuiltInParameter)(-1001364);
			}
			if (val2.HasValue)
			{
				Parameter val3 = elem.get_Parameter(val2.Value);
				if (val3 != null)
				{
					return val3;
				}
				if ((long)val2.Value == -1001364)
				{
					val3 = elem.get_Parameter((BuiltInParameter)(-1001357));
					if (val3 != null)
					{
						return val3;
					}
				}
			}
		}
		string[] array = new string[8] { "Offset", "偏移", "Base Offset", "底部偏移", "Elevation from Level", "相对标高", "标高偏移", "偏移量" };
		foreach (string text in array)
		{
			Parameter val4 = elem.LookupParameter(text);
			if (val4 != null && (int)val4.StorageType == 2)
			{
				return val4;
			}
		}
		return null;
	}
}
