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
      Utils.Logger.LogDebug("[CsvParse] CSV 데이터 파싱을 시작합니다.");

      NodeCsvParse();
      Utils.Logger.LogDebug($"[CsvParse] Node 파싱 완료. 총 {this.nodeInstance.GetNodeCount()}개의 노드 확보.");

      ElementCsvParse();
      Utils.Logger.LogDebug($"[CsvParse] Element 파싱 완료. 총 {this.elementInstance.GetCount()}개의 요소 확보.");

      BoundaryCondition();
      Utils.Logger.LogDebug($"[CsvParse] 경계조건 설정 완료. Z축 최하단 노드 {BoundaryCondition_list.Count}개 추출.");
    }

    public void NodeCsvParse()
    {
      // CSV 읽어오기
      var lines = File.ReadAllLines(this.nodeCsv);
      int currentLine = 0; // 오류 발생 위치 추적용

      foreach (var line in lines)
      {
        currentLine++;
        try
        {
          string[] values = line.Split(',');

          // 헤더 등 빈칸이나 잘못된 값이 들어올 경우를 대비한 유효성 검사 추가 가능
          if (values.Length < 5) continue;

          int nodeID = int.Parse(values[1]);
          double X = double.Parse(values[2]);
          double Y = double.Parse(values[3]);
          double Z = double.Parse(values[4]);

          this.nodeInstance.AddWithID(nodeID, X, Y, Z);

          // Leg Lifting의 순서를 legLiftingOrder에 저장
          if (values.Length > 5 && int.TryParse(values[5], out int legLiftingOrder))
          {
            if (!this.legLiftingOrder.ContainsKey(legLiftingOrder))
            {
              this.legLiftingOrder[legLiftingOrder] = new List<int>();
            }
            this.legLiftingOrder[legLiftingOrder].Add(nodeID);
          }
        }
        catch (Exception ex)
        {
          // 어느 파일의 몇 번째 줄에서 파싱 에러가 발생했는지 구체적으로 로깅
          Utils.Logger.LogError($"NodeCsvParse 오류: {this.nodeCsv} 파일의 {currentLine}번째 줄 파싱 실패. (내용: {line})", ex);
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
          bool isMatched = false;
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
              };
              this.elementInstance.AddWithID(elementID, new List<int> { nodeA, nodeB }, propertyID, extraData);
              isMatched = true;
              break; // 매칭되었으면 더 이상 찾을 필요 없음
            }
          }

          // 🔹 디버그: 매칭되는 Property가 없어 누락된 부재 확인
          if (!isMatched)
          {
            Utils.Logger.LogDebug($"[CsvParse 경고] Element ID {elementID}의 Property('{PropertyInput}')가 변환 사전에 존재하지 않습니다.");
          }
        }
      }
    }

    public void BoundaryCondition()
    {
      // Z축 기준 가장 낮은 값
      double minZ = this.nodeInstance.Min(node => node.Value.Z);

      double epsilon = 0.001;
      BoundaryCondition_list = this.nodeInstance
          .Where(n => Math.Abs(n.Value.Z - minZ) < epsilon)
          .Select(n => n.Key).ToList();

    }
  }
}
