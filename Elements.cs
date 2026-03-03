using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TrussModelBuilder.Model;


namespace TrussModelBuilder.Model
{
  // Element의 Local Axis를 계산하기 위한 클래스 
  class BeamLocalAxis
  {
    public Nodes NodeInstance { get; }

    public BeamLocalAxis(Nodes NodeInstance)
    {
      this.NodeInstance = NodeInstance;
    }

    private double[] Normalize(double[] vec)
    {
      double mag = Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1] + vec[2] * vec[2]);
      return mag == 0 ? new double[] { 0, 0, 0 } : vec.Select(v => v / mag).ToArray();
    }

    private double[] CrossProduct(double[] a, double[] b)
    {
      return new double[]
      {
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0]
      };
    }

    public double[] CalculateLocalXVector(int nodeA, int nodeB)
    {
      Point3D PositionA = this.NodeInstance[nodeA];
      Point3D PositionB = this.NodeInstance[nodeB];

      double[] xvec = { PositionB.X - PositionA.X, PositionB.Y - PositionA.Y, PositionB.Z - PositionA.Z };
      return Normalize(xvec);
    }

    public double[] CalculateNormalVector(int nodeA, int nodeB)
    {
      double[] localX = CalculateLocalXVector(nodeA, nodeB);

      // 기준 벡터 선택
      double[] refVector;
      if (Math.Abs(localX[2]) > 0.99)  // Z 방향 부재인 경우
      {
        refVector = new double[] { 1, 0, 0 };  // X축을 기준으로 선택
      }
      else
      {
        refVector = new double[] { 0, 0, 1 };  // 기본적으로 Z축을 기준으로 선택
      }

      // 기준 벡터와 로컬 X축의 외적을 사용하여 법선 벡터 계산
      double[] normalVec = CrossProduct(refVector, localX);
      return Normalize(normalVec);
    }

    public double[] CalculateLocalZVector(int nodeA, int nodeB)
    {
      double[] localX = CalculateLocalXVector(nodeA, nodeB);
      double[] localY = CalculateNormalVector(nodeA, nodeB);

      // X축과 Y축의 외적으로 Z축 계산
      double[] localZ = CrossProduct(localX, localY);
      return Normalize(localZ);
    }
  }

  // 요소 속성 정의 (연결된 노드 ID 및 속성 ID)
  public struct ElementAttribute
  {
    public List<int> NodeIDs { get; }
    public int PropertyID { get; }
    public Properties Properties { get; }
    public Nodes NodeInstance { get; }
    public double[] LocalAxis { get; }
    // 기타 데이터를 담기 위한 컨테이너 
    public Dictionary<string, string> ExtraData { get; set; }

    public ElementAttribute(List<int> nodeIDs, int propertyID, Properties properties, Nodes nodeInstance,
      Dictionary<string, string> extraData = null)
    {
      NodeIDs = nodeIDs;
      PropertyID = propertyID;
      Properties = properties;
      NodeInstance = nodeInstance;

      var localAxis = new BeamLocalAxis(NodeInstance);
      this.LocalAxis = localAxis.CalculateLocalZVector(NodeIDs[0], NodeIDs[1]);

      ExtraData = extraData ?? new Dictionary<string, string>();
    }

    public override string ToString()
    {
      string extraInfo = (ExtraData != null && ExtraData.Count > 0)
          ? string.Join(", ", ExtraData.Select(kv => $"{kv.Key}: {kv.Value}"))
          : "None";
      return $"Nodes: [{string.Join(", ", NodeIDs)}], PropertyID: {PropertyID}, ExtraData: {extraInfo}";
    }
  }

  public class Elements : IEnumerable<KeyValuePair<int, ElementAttribute>> // IEnumerable 구현 (foreach 지원)
  {
    public Nodes Nodes { get; private set; } // Nodes 객체 참조
    public Properties Properties { get; private set; } // Properties 객체 참조

    public int elementID = 0; // 요소 ID 카운터
    private Dictionary<int, ElementAttribute> elements = new Dictionary<int, ElementAttribute>(); // 요소 저장
    private Dictionary<string, int> elementLookup = new Dictionary<string, int>(); // 요소 중복 방지용

    public Elements(Nodes nodes, Properties properties)
    {
      Nodes = nodes;
      Properties = properties;

    }

    // 🔹 인덱서 추가 (element_cls[elementID] 형태로 요소 조회 가능)
    public ElementAttribute this[int elementID]
    {
      get
      {
        if (!elements.ContainsKey(elementID))
        {
          throw new KeyNotFoundException($"Element ID {elementID} does not exist.");
        }
        return elements[elementID];
      }
    }

    // 요소 추가 (중복 방지: 동일한 nodeIDs + propertyID가 존재하면 기존 ID 반환)
    public int AddOrGet(List<int> nodeIDs, int propertyID, Dictionary<string, string> extraData = null)
    {
      nodeIDs.Sort(); // 리스트 정렬 (중복 검사 시 순서 영향을 없애기 위함)
      string key = $"{string.Join(",", nodeIDs)}|{propertyID}";

      if (elementLookup.TryGetValue(key, out int existingElementID))
      {
        return existingElementID; // 기존 요소 ID 반환
      }

      elementID++;
      ElementAttribute newElement = new ElementAttribute(nodeIDs, propertyID, Properties, Nodes, extraData);
      elements[elementID] = newElement;
      elementLookup[key] = elementID;

      return elementID;
    }

    // 임의 ID로 Element 요소 추가 
    public void AddWithID(int eleID, List<int> nodeIDs, int propertyID, Dictionary<string, string> extraData = null)
    {
      string key = $"{string.Join(",", nodeIDs)}|{propertyID}";

      ElementAttribute newElement = new ElementAttribute(nodeIDs, propertyID, Properties, Nodes, extraData);
      elements[eleID] = newElement;
      elementLookup[key] = eleID;
      elementID = eleID; // 인스턴스의 현대 elementID를 맞춰준다. 
    }

    // 요소 제거 (존재하지 않으면 예외 발생)
    public void Remove(int inputElementID)
    {
      if (!elements.ContainsKey(inputElementID))
      {
        throw new KeyNotFoundException($"Element ID {inputElementID} does not exist.");
      }

      ElementAttribute removedElement = elements[inputElementID];
      string key = $"{string.Join(",", removedElement.NodeIDs)}|{removedElement.PropertyID}";

      elements.Remove(inputElementID);
      elementLookup.Remove(key);
    }

    // 마지막 추가된 요소의 ID 반환
    public int GetLastID()
    {
      return elementID > 0 ? elementID : throw new InvalidOperationException("No elements exist.");
    }

    // 요소 개수 반환
    public int GetCount()
    {
      return elements.Count;
    }

    // 특정 속성을 가진 요소 검색
    public List<int> FindElementsByProperty(int propertyID)
    {
      return elements
          .Where(element => element.Value.PropertyID == propertyID)
          .Select(element => element.Key)
          .ToList();
    }

    // Element 요소들을 특정 방향으로 복사하여 이동
    public List<int> ElementsCopyMove(List<int> moveElements, double X, double Y, double Z,
   ref List<List<int>> HorizontalConnectionElements_list)
    {
      var newElementID_list = new List<int>();
      Dictionary<int, int> newNodeConnection_dict = new Dictionary<int, int>();

      foreach (var elementID in moveElements)
      {
        if (!elements.ContainsKey(elementID))
        {
          throw new KeyNotFoundException($"Element ID {elementID} does not exist.");
        }

        ElementAttribute eleAttribute = elements[elementID];
        int propertyID = eleAttribute.PropertyID;

        var copyNodeIDs = new List<int>(eleAttribute.NodeIDs);
        int newNodeA, newNodeB;

        // 기존 노드 ID를 새로운 노드 ID로 변환하는 과정
        if (!newNodeConnection_dict.ContainsKey(copyNodeIDs[0]))
        {
          Point3D Position = Nodes[copyNodeIDs[0]];
          newNodeA = Nodes.AddOrGet(Position.X + X, Position.Y + Y, Position.Z + Z);
          newNodeConnection_dict[copyNodeIDs[0]] = newNodeA;
        }
        else
        {
          newNodeA = newNodeConnection_dict[copyNodeIDs[0]];
        }

        if (!newNodeConnection_dict.ContainsKey(copyNodeIDs[1]))
        {
          Point3D Position = Nodes[copyNodeIDs[1]];
          newNodeB = Nodes.AddOrGet(Position.X + X, Position.Y + Y, Position.Z + Z);
          newNodeConnection_dict[copyNodeIDs[1]] = newNodeB;
        }
        else
        {
          newNodeB = newNodeConnection_dict[copyNodeIDs[1]];  // 여기서 copyNodeIDs[1]을 참조해야 함
        }

        // 새로운 Element 추가
        int newElementID = AddOrGet(new List<int> { newNodeA, newNodeB }, propertyID);
        newElementID_list.Add(newElementID);
      }

      // 수평 연결 요소 업데이트
      foreach (var entry in newNodeConnection_dict)
      {
        HorizontalConnectionElements_list.Add(new List<int> { entry.Key, entry.Value });
      }

      return newElementID_list;
    }

    public double CalculateDistanceFromLine(int nodeA, int nodeB, int nodeC)
    {
      Point3D A = Nodes[nodeA];
      Point3D B = Nodes[nodeB];
      Point3D C = Nodes[nodeC];

      double[] AB = { B.X - A.X, B.Y - A.Y, B.Z - A.Z };
      double[] AC = { C.X - A.X, C.Y - A.Y, C.Z - A.Z };

      // 벡터 AB의 크기 (노말라이즈를 위한 값)
      double AB_mag = Math.Sqrt(AB[0] * AB[0] + AB[1] * AB[1] + AB[2] * AB[2]);
      if (AB_mag == 0) return double.NaN; // 두 노드가 동일할 경우 예외 처리

      // AC 벡터를 AB 벡터 위에 투영
      double projectionFactor = (AC[0] * AB[0] + AC[1] * AB[1] + AC[2] * AB[2]) / (AB_mag * AB_mag);

      // 📌 **투영점이 선분 범위 [0, 1] 사이에 있는지 검사**
      if (projectionFactor < 0 || projectionFactor > 1)
        return -1; // 📌 수선의 발이 선분 범위 밖이면 -1 반환

      // 투영점의 좌표
      double[] projection = {
        A.X + projectionFactor * AB[0],
        A.Y + projectionFactor * AB[1],
        A.Z + projectionFactor * AB[2]
    };

      // nodeC와 수선의 발(projection) 사이 거리 계산
      return Math.Sqrt(Math.Pow(C.X - projection[0], 2) +
                       Math.Pow(C.Y - projection[1], 2) +
                       Math.Pow(C.Z - projection[2], 2));
    }


    // 두 개의 요소에 대해 각 노드와 다른 요소의 직선 간 거리를 계산
    public Dictionary<string, double> CalculateDistanceBetweenElements(int elementA_ID, int elementB_ID)
    {
      if (!elements.ContainsKey(elementA_ID) || !elements.ContainsKey(elementB_ID))
        throw new KeyNotFoundException("Element ID가 존재하지 않습니다.");

      ElementAttribute elementA = elements[elementA_ID];
      ElementAttribute elementB = elements[elementB_ID];

      if (elementA.NodeIDs.Count < 2 || elementB.NodeIDs.Count < 2)
        throw new InvalidOperationException("각 요소는 최소 두 개의 노드를 포함해야 합니다.");

      int nodeA = elementA.NodeIDs[0];
      int nodeB = elementA.NodeIDs[1];
      int nodeC = elementB.NodeIDs[0];
      int nodeD = elementB.NodeIDs[1];

      if (new List<int> { nodeA, nodeB }.Contains(nodeC)
        | new List<int> { nodeA, nodeB }.Contains(nodeD)
        | new List<int> { nodeC, nodeD }.Contains(nodeA)
        | new List<int> { nodeC, nodeD }.Contains(nodeB))
      {
        return new Dictionary<string, double>(); // 빈 Dictionary 반환하여 건너뛰기
      }

      Dictionary<string, double> distances = new Dictionary<string, double>
            {
                { "nodeC_to_lineAB", CalculateDistanceFromLine(nodeA, nodeB, nodeC) },
                { "nodeD_to_lineAB", CalculateDistanceFromLine(nodeA, nodeB, nodeD) },
                { "nodeA_to_lineCD", CalculateDistanceFromLine(nodeC, nodeD, nodeA) },
                { "nodeB_to_lineCD", CalculateDistanceFromLine(nodeC, nodeD, nodeB) }
            };

      return distances;
    }

    // Node 2개를 넣으면 Element를 찾을수 있는 메써드
    public int FindElementByNodeIDs(int nodeID1, int nodeID2)
    {
      // 노드 ID들을 정렬하여 순서가 달라도 동일하게 처리할 수 있도록 함
      var sortedNodeIDs = new List<int> { nodeID1, nodeID2 };
      sortedNodeIDs.Sort();

      // 요소들을 순회하며, 노드 ID들이 일치하는 요소를 찾음
      foreach (var element in elements)
      {
        var elementNodeIDs = element.Value.NodeIDs;
        var sortedElementNodeIDs = new List<int>(elementNodeIDs);
        sortedElementNodeIDs.Sort();

        if (sortedElementNodeIDs.SequenceEqual(sortedNodeIDs))
        {
          return element.Key; // 첫 번째로 일치하는 요소 ID 반환
        }
      }

      throw new KeyNotFoundException("해당하는 요소를 찾을 수 없습니다.");
    }




    // 요소 ID 기반 전체 요소 리스트 반환
    public List<KeyValuePair<int, ElementAttribute>> GetAllElements()
    {
      return new List<KeyValuePair<int, ElementAttribute>>(elements);
    }

    // 🔹 IEnumerable<ElementAttribute> 인터페이스 구현 (foreach 지원)
    public IEnumerator<KeyValuePair<int, ElementAttribute>> GetEnumerator()
    {
      return elements.GetEnumerator();
    }

    // 🔹 IEnumerable 인터페이스 구현 (비제네릭 버전)
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
