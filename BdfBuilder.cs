using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrussModelBuilder.Model;

namespace TrussModelBuilder.Control
{
  public class BdfBuilder
  {
    public int sol;
    public Materials materialInstance;
    public Properties propertyInstance;
    public Nodes nodeInstance;
    public Elements elementInstance;
    public List<int> BoundaryCondition_list;
    public string propertyMaterialReferenceBDF;

    // BDF에 출력된 텍스트 모음 리스트 
    public List<String> BdfLines = new List<String>();

    public BdfBuilder(
      int Sol, Materials materialInstance, Properties propertyInstance,
      Nodes nodeInstance, Elements elementInstance, List<int> BoundaryCondition_list,
      string propertyMaterialReferenceBDF)
    {
      this.sol = Sol;
      this.materialInstance = materialInstance;
      this.propertyInstance = propertyInstance;
      this.nodeInstance = nodeInstance;
      this.elementInstance = elementInstance;
      this.BoundaryCondition_list = BoundaryCondition_list;
      this.propertyMaterialReferenceBDF = propertyMaterialReferenceBDF;
    }

    public void Run()
    {
      // 01. 해석 종류 설정
      ExecutiveControlSection();
      // 02. 출력결과 종류 설정, 하중, 경계조건 ID 설정
      CaseControlSection();
      // 03. Node, Element 데이터 입력
      NodeElementSection();
      // 04. Property, Material은 propertyMaterialReferenceBDF 읽어와서 그대로 붙여준다
      PropertyMaterialSection();
      // 05. 경계조건 구현
      SpcSection();
      // 06. 하중조건 구현
      LoadSection("GRAV", new double[] { 0.02, 0.02, -1.0 });
      EndData();
    }

    public void ExecutiveControlSection()
    {
      BdfLines.Add(FormatField($"SOL {this.sol}"));
      BdfLines.Add(FormatField($"CEND"));
    }

    public void CaseControlSection()
    {
      BdfLines.Add("DISPLACEMENT = ALL");
      BdfLines.Add("ELFORCE = ALL");
      BdfLines.Add("SPCFORCES = ALL");
      BdfLines.Add("STRESS = ALL");
      BdfLines.Add("SUBCASE 1");
      BdfLines.Add("    LABEL = Load Case 1");
      BdfLines.Add("    SPC = 1");
      BdfLines.Add("    LOAD = 2");
      BdfLines.Add("    ANALYSIS = STATICS");
      BdfLines.Add("BEGIN BULK");
      BdfLines.Add("PARAM,POST,-1");
    }

    public void NodeElementSection()
    {
      foreach (var node in this.nodeInstance)
      {
        string nodeText = $"{FormatField("GRID")}"
          + $"{FormatField(node.Key, "right")}"
          + $"{FormatField("")}"
          + $"{FormatField(node.Value.X, "right")}"
          + $"{FormatField(node.Value.Y, "right")}"
          + $"{FormatField(node.Value.Z, "right")}";
        BdfLines.Add(nodeText);
      }

      foreach (var element in this.elementInstance)
      {
        string elementText = $"{FormatField("CBAR")}"
         + $"{FormatField(element.Key, "right")}"
         + $"{FormatField(element.Value.PropertyID, "right")}"
         + $"{FormatField(element.Value.NodeIDs[0], "right")}"
         + $"{FormatField(element.Value.NodeIDs[1], "right")}"
         + $"{FormatField(element.Value.LocalAxis[0], "right")}"
         + $"{FormatField(element.Value.LocalAxis[1], "right")}"
         + $"{FormatField(element.Value.LocalAxis[2], "right")}"
         + $"{FormatField("BGG", "right")}";
        BdfLines.Add(elementText);
      }
    }

    public void PropertyMaterialSection()
    {
      // CSV 읽어오기
      var lines = File.ReadAllLines(this.propertyMaterialReferenceBDF);
      BdfLines.AddRange(lines);
    }

    public void SpcSection()
    {
      foreach(var bound in BoundaryCondition_list)
      {
        string boundText = $"{FormatField("SPC")}"
            + $"{FormatField("1", "right")}"
            + $"{FormatField(bound, "right")}"
            + $"{FormatField(123456, "right")}"
            + $"{FormatField(0.0, "right")}";

        BdfLines.Add(boundText);
      }
    }

    public void LoadSection(string type, double[] Value)
    {
      if (type == "GRAV")
      {
        string loadText = $"{FormatField(type)}"
           + $"{FormatField("2", "right")}"
           + $"{FormatField("")}"
           + $"{FormatField(9800.0, "right")}"
           + $"{FormatField(Value[0], "right")}"
           + $"{FormatField(Value[1], "right")}"
           + $"{FormatField(Value[2], "right")}";

        BdfLines.Add(loadText);
      }
    }

    public void EndData()
    {
      BdfLines.Add("ENDDATA");
    }

    // 하나의 문자열을 8칸에 넣어서 문자열을 반환하는 메써드
    public string FormatField(string data, string direction = "left")
    {
      if (direction == "right")
      {
        return data.PadLeft(8).Substring(0, 8);
      }

      return data.PadRight(8).Substring(0, 8);
    }

    // ✅ int 지원
    public string FormatField(int data, string direction = "left")
    {
      return FormatField(data.ToString(), direction);
    }

    public string FormatField(double data, string direction = "left", bool isRho = false)
    {
      if (isRho)
      {
        return FormatField(ConvertScientificNotation(data), direction);
      }
      return FormatField(data.ToString("0.00"), direction);  // 기본적으로 소수점 2자리
    }

    // ✅ 지수 표기법 변환 (E-표기법 → "-지수" 형태)
    private string ConvertScientificNotation(double data)
    {
      string scientific = data.ToString("0.00E+0");  // "7.85E-09" 형식
      if (scientific.Contains("E"))
      {
        string[] parts = scientific.Split('E'); // ["7.85", "-09"]
        return $"{parts[0]}{int.Parse(parts[1])}"; // "7.85-9"
      }
      return scientific;
    }
  }
}
