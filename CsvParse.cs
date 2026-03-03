using System;

using System.Collections;
using System.IO;
using TrussModelBuilder.Model;


namespace TrussModelBuilder.Control
{
  // property를 변환할때 참조하는 TrussPropertyCovert.txt을 불러와서 구조체에 값을 입력하고 향후 참조 
  public struct PropertyConvertInfo
  {
    public string PropertyName { get; set; }
    public int PropertyID { get; set; }
    public string Group { get; set; }

    public PropertyConvertInfo(string propertyName, int propertyID, string group)
    {
      PropertyName = propertyName;
      PropertyID = propertyID;
      Group = group;
    }

    public override string ToString()
    {
      return $"(PropertyName:{PropertyName}, PropertyID:{PropertyID}, Group:{Group})";
    }

  }


  public class CsvParse
  {
    public string nodeCsv;
    public string wayCsv;
    public Materials materialInstance;
    public Properties propertyInstance;
    public Nodes nodeInstance;
    public Elements elementInstance;
    public string propertyConvertTxt;

    public List<int> BoundaryCondition_list = new List<int>();

    // LegLifting 순서를 저장하는 딕셔너리
    public Dictionary<int, List<int>> legLiftingOrder = new Dictionary<int, List<int>>();
    // 제대로 Truss 부재의 Property 변환이 안되는 경우를 모아둔다.
    public List<string> ErrorPropertyConvert = new List<string>();
    // TrussPropertyCovert.txt을 읽어와서 변환되어야 하는 데이터를 딕셔너리에 정리한다. 
    public Dictionary<string, PropertyConvertInfo> PropertyConverReference 
      = new Dictionary<string, PropertyConvertInfo>();


    public CsvParse(string nodeCsv, string wayCsv, Materials materialInstance, Properties propertyInstance,
      Nodes nodeInstance, Elements elementInstance, string propertyConvertTxt)
    {
      this.nodeCsv = nodeCsv;
      this.wayCsv = wayCsv;
      this.materialInstance = materialInstance;
      this.propertyInstance = propertyInstance;
      this.nodeInstance = nodeInstance;
      this.elementInstance = elementInstance;
      this.propertyConvertTxt = propertyConvertTxt;
    }

    public void Run()
    {
      // 01. Node.csv를 파싱하여 Node 정보를 가지고 온다.
      NodeCsvParse();
      // 02. Element.csv를 파싱하여 Element 정보를 가지고 온다. 
      ElementCsvParse();
      // 03. Truss의 경계조건 설정. 여기서는 Z축으로 가장 낮은 값을 가지는 Node로 지정
      BoundaryCondition();
    }

    public void NodeCsvParse()
    {
      // CSV 읽어오기
      var lines = File.ReadAllLines(this.nodeCsv);

      foreach (var line in lines)
      {
        string[] values = line.Split(',');
        int nodeID = int.Parse(values[1]);
        double X = double.Parse(values[2]);
        double Y = double.Parse(values[3]);
        double Z = double.Parse(values[4]);       

        this.nodeInstance.AddWithID(nodeID, X, Y, Z);

        // Leg Lifting의 순서를 legLiftingOrder에 저장
        if (int.TryParse(values[5], out int legLiftingOrder))
        {
          if (!this.legLiftingOrder.ContainsKey(legLiftingOrder))
          {
            this.legLiftingOrder[legLiftingOrder] = new List<int>();
          }
          this.legLiftingOrder[legLiftingOrder].Add(nodeID);
        }
      }
    }

    public void ElementCsvParse()
    {
      // CSV 읽어오기
      var lines = File.ReadAllLines(this.wayCsv);
      var propertyConvertlines = File.ReadAllLines(this.propertyConvertTxt);
      
      // Truss 부재 변환의 정보들을 먼저 정리해둔다.
      foreach(var line in propertyConvertlines)
      {
        string[] values = line.Split(',');
        string Key = values[0];
        string propertyName = values[1];        
        int propertyID = int.Parse(values[2]);
        string group = values[4];

        PropertyConverReference[Key] = new PropertyConvertInfo
        {
          PropertyName = propertyName,
          PropertyID = propertyID,
          Group = group
        };    
      }

      foreach (var line in lines)
      {
        string[] values = line.Split(',');
        int elementID = int.Parse(values[1]);
        string PropertyInput = values[2];

        // nodeA가 정수로 변환 안되면 건너뛰기
        string nodeA_Input = values[3];
        if (!int.TryParse(nodeA_Input, out int nodeA))
        {
          continue;
        }

        // nodeB가 정수로 변환 안되면 건너뛰기
        string nodeB_Input = values[4];
        if (!int.TryParse(nodeB_Input, out int nodeB))
        {
          continue;
        }

        // Property 속성이 NONE으로 되어있다면 Element 만들지 않고 건너뛴다. 
        if (PropertyInput.ToUpper() == "NONE")
        {
          continue;
        }
        else // Property 분류 작업
        {
          foreach (var key in PropertyConverReference.Keys)
          {
            if (PropertyInput.StartsWith(key))
            {
              int propertyID = PropertyConverReference[key].PropertyID;
              string propertyName = PropertyConverReference[key].PropertyName;
              string group = PropertyConverReference[key].Group;
              Dictionary<string, string> extraData = new Dictionary<string, string>
              {
                {"propertyName", propertyName},
                {"Group", group }
              }
                ;
              this.elementInstance.AddWithID(elementID, new List<int> { nodeA, nodeB }, propertyID, extraData); 
            }          
          }         
        }
      }
    }

    public void BoundaryCondition()
    {
      // Z축 기준 가장 낮은 값
      double minZ = this.nodeInstance.Min(node => node.Value.Z);

      BoundaryCondition_list = this.nodeInstance.Where(n => n.Value.Z == minZ)
        .Select(n => n.Key).ToList();     

    }
  }
}
