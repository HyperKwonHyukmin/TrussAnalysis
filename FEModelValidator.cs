using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TrussModelBuilder.Model;

namespace TrussModelBuilder.Control
{
  public class FEModelValidator
  {
    public Materials materialInstance;
    public Properties propertyInstance;
    public Nodes nodeInstance;
    public Elements elementInstance;

    public FEModelValidator(Materials materialInstance, Properties propertyInstance, Nodes nodeInstance, Elements elementInstance)
    {
      this.materialInstance = materialInstance;
      this.propertyInstance = propertyInstance;
      this.nodeInstance = nodeInstance;
      this.elementInstance = elementInstance;
    }

    public void Run()
    {
      // Element의 두 node 사이에 node가 존재한다면 그 Node 중심으로 Element 쪼개는 작업
      SeparateElementByNodes();
      // 선장 설계 요청으로 Truss 부재의 Mesh 사이즈 조절
      CustomMeshGenerator();
    }


    private void AddSplitNode(Dictionary<int, List<int>> elementsToSplit, int key, int splitNode)
    {
      if (!elementsToSplit.TryGetValue(key, out List<int> splitList))
      {
        splitList = new List<int>();
        elementsToSplit[key] = splitList;
      }
      if (!splitList.Contains(splitNode))
      {
        splitList.Add(splitNode);
      }
    }

    public void SeparateElementByNodes()
    {
      // 고정 임계값: 0.1 이내는 0으로 간주
      double distanceThreshold = 1;

      // 각 요소의 두 노드 좌표를 이용해 bounding box를 미리 계산합니다.
      // (minX, minY, minZ, maxX, maxY, maxZ)
      Dictionary<int, (double minX, double minY, double minZ, double maxX, double maxY, double maxZ)> boundingBoxes =
          new Dictionary<int, (double, double, double, double, double, double)>();

      List<int> elementsToProcess = this.elementInstance.GetAllElements().Select(e => e.Key).ToList();

      foreach (int elementId in elementsToProcess)
      {
        var nodeIDs = this.elementInstance[elementId].NodeIDs;
        int nodeA = nodeIDs[0];
        int nodeB = nodeIDs[1];
        Point3D pointA = this.nodeInstance[nodeA];
        Point3D pointB = this.nodeInstance[nodeB];

        double minX = Math.Min(pointA.X, pointB.X);
        double minY = Math.Min(pointA.Y, pointB.Y);
        double minZ = Math.Min(pointA.Z, pointB.Z);
        double maxX = Math.Max(pointA.X, pointB.X);
        double maxY = Math.Max(pointA.Y, pointB.Y);
        double maxZ = Math.Max(pointA.Z, pointB.Z);

        boundingBoxes[elementId] = (minX, minY, minZ, maxX, maxY, maxZ);
      }

      // 분할할 요소와 해당 분할 노드를 저장할 딕셔너리 (elementID -> List<splitNodeIDs>)
      Dictionary<int, List<int>> elementsToSplit = new Dictionary<int, List<int>>();

      for (int i = 0; i < elementsToProcess.Count; i++)
      {
        int eleA_ID = elementsToProcess[i];
        var nodesA = this.elementInstance[eleA_ID].NodeIDs;
        // 요소 A의 두 노드 좌표
        Point3D A = this.nodeInstance[nodesA[0]];
        Point3D B = this.nodeInstance[nodesA[1]];
        // 여기서는 길이에 관계없이 고정 임계값 사용
        double effectiveTolA = distanceThreshold;

        for (int j = i + 1; j < elementsToProcess.Count; j++)
        {
          int eleB_ID = elementsToProcess[j];

          // Bounding Box 필터: 두 요소의 bounding box가 겹치지 않으면 계산 생략
          var bboxA = boundingBoxes[eleA_ID];
          var bboxB = boundingBoxes[eleB_ID];
          bool overlap = !(bboxA.maxX < bboxB.minX - distanceThreshold ||
                           bboxA.minX > bboxB.maxX + distanceThreshold ||
                           bboxA.maxY < bboxB.minY - distanceThreshold ||
                           bboxA.minY > bboxB.maxY + distanceThreshold ||
                           bboxA.maxZ < bboxB.minZ - distanceThreshold ||
                           bboxA.minZ > bboxB.maxZ + distanceThreshold);
          if (!overlap)
            continue;

          var nodesB = this.elementInstance[eleB_ID].NodeIDs;
          // 두 요소가 공통 노드를 가지면 비교하지 않음 (노드가 2개인 경우 단순 비교)
          if (nodesA[0] == nodesB[0] || nodesA[0] == nodesB[1] ||
              nodesA[1] == nodesB[0] || nodesA[1] == nodesB[1])
            continue;

          // 요소 B의 두 노드 좌표, 여기서도 고정 임계값 사용
          double effectiveTolB = distanceThreshold;

          var distances = this.elementInstance.CalculateDistanceBetweenElements(eleA_ID, eleB_ID);


          if (distances.TryGetValue("nodeC_to_lineAB", out double distC) && Math.Abs(distC) < effectiveTolA)
          {
            AddSplitNode(elementsToSplit, eleA_ID, nodesB[0]);
          }
          if (distances.TryGetValue("nodeD_to_lineAB", out double distD) && Math.Abs(distD) < effectiveTolA)
          {
            AddSplitNode(elementsToSplit, eleA_ID, nodesB[1]);
          }
          if (distances.TryGetValue("nodeA_to_lineCD", out double distA) && Math.Abs(distA) < effectiveTolB)
          {
            AddSplitNode(elementsToSplit, eleB_ID, nodesA[0]);
          }
          if (distances.TryGetValue("nodeB_to_lineCD", out double distB) && Math.Abs(distB) < effectiveTolB)
          {
            AddSplitNode(elementsToSplit, eleB_ID, nodesA[1]);
          }
        }
      }

      // 최종적으로 분할 대상 요소에 대해 SplitElementByNodes 호출
      int splitCount = 0;
      foreach (var kv in elementsToSplit)
      {
        SplitElementByNodes(kv.Key, kv.Value);
        splitCount++;
      }

      // 쪼개진 기존 Element 삭제 
      foreach (var ele in elementsToSplit)
      {
        this.elementInstance.Remove(ele.Key);
      }

      Utils.Logger.LogDebug($"[Validator] 노드 간섭에 의한 요소 분할 처리 완료. 총 {splitCount}개의 원본 소가 분할되었습니다.");

    }


    private List<int> SplitElementByNodes(int elementID, List<int> splitNodes)
    {
      List<int> splitedElements = new List<int>();
      if (splitNodes.Count == 0)
      {
        //Console.WriteLine($"⚠️ Warning: No split nodes for element {elementID}. Skipping split operation.");
        return splitedElements;
      }
      //Console.WriteLine($"{elementID}, {string.Join(", ", splitNodes)}");

      int nodeA = this.elementInstance[elementID].NodeIDs[0];
      int nodeB = this.elementInstance[elementID].NodeIDs[1];
      double nodeA_X = this.nodeInstance[nodeA].X;
      double nodeA_Y = this.nodeInstance[nodeA].Y;
      double nodeA_Z = this.nodeInstance[nodeA].Z;
      int propertyID = this.elementInstance[elementID].PropertyID;
      Dictionary<string, string> ExtraData = this.elementInstance[elementID].ExtraData;


      // splitNodes를 nodeA와의 거리 기준으로 정렬 : Element 생성 순서를 지정하기 위해
      splitNodes.Sort((node1, node2) =>
      {
        double dx1 = this.nodeInstance[node1].X - nodeA_X;
        double dy1 = this.nodeInstance[node1].Y - nodeA_Y;
        double dz1 = this.nodeInstance[node1].Z - nodeA_Z;
        double dx2 = this.nodeInstance[node2].X - nodeA_X;
        double dy2 = this.nodeInstance[node2].Y - nodeA_Y;
        double dz2 = this.nodeInstance[node2].Z - nodeA_Z;
        double distSq1 = dx1 * dx1 + dy1 * dy1 + dz1 * dz1;
        double distSq2 = dx2 * dx2 + dy2 * dy2 + dz2 * dz2;
        return distSq1.CompareTo(distSq2);
      });

      // 일단 쪼개진 첫번째 Element 생성
      int newElementID = this.elementInstance.AddOrGet(new List<int> { nodeA, splitNodes[0] }, propertyID, ExtraData);
      splitedElements.Add(newElementID);

      for (int i = 0; i < splitNodes.Count - 1; i++)
      {
        newElementID = this.elementInstance.AddOrGet(new List<int> { splitNodes[i], splitNodes[i + 1] }, propertyID, ExtraData);
        splitedElements.Add(newElementID);
      }
      newElementID = this.elementInstance.AddOrGet(new List<int> { splitNodes[^1], nodeB }, propertyID, ExtraData);
      splitedElements.Add(newElementID);

      return splitedElements;
    }

    public void CustomMeshGenerator()
    {
      // 향후 삭제할 Element들을 모아둘 리스트 
      var elementList = this.elementInstance.ToList();  // Dictionary를 List로 변환하여 순회
      // 
      var newElementList = new List<int>();

      Dictionary<int, (int startNodeID, List<double> vector, double quotient, int divideCount)> customMeshInfo =
            new Dictionary<int, (int, List<double>, double, int)>();

      for (int i = 0; i < elementList.Count; i++)
      {
        var ele = elementList[i];

        // Girder의 경우 종방향, 횡방향 모두 850mm 기준으로 mesh 쪼개기
        if (ele.Value.ExtraData["Group"].Trim().Equals("Girder") | ele.Value.ExtraData["Group"].Trim().Equals("Beam"))
        {
          double Tolerance = 850.0;
          int nodeA = ele.Value.NodeIDs[0];
          int nodeB = ele.Value.NodeIDs[1];
          double distance = FEModelValidator.DistanceBetweenTwoNodes(nodeA, nodeB, nodeInstance);
          List<double> vector = FEModelValidator.VectorOfTwoNodes(nodeA, nodeB, nodeInstance);

          // divideCount 계산 최적화
          int divideCount = (int)Math.Ceiling(distance / Tolerance) - 1;
          if (divideCount > 0)
          {
            double quotient = distance / (divideCount + 1);
            customMeshInfo.Add(ele.Key, (startNodeID: nodeA, vector: vector, quotient: quotient, divideCount: divideCount));
          }
        }

        // Colume의 경우 900mm 기준으로 Mesh 쪼개기
        else if (ele.Value.ExtraData["Group"].Trim().Equals("Column"))
        {
          double Tolerance = 900.0;
          int nodeA = ele.Value.NodeIDs[0];
          int nodeB = ele.Value.NodeIDs[1];
          double distance = FEModelValidator.DistanceBetweenTwoNodes(nodeA, nodeB, nodeInstance);
          List<double> vector = FEModelValidator.VectorOfTwoNodes(nodeA, nodeB, nodeInstance);

          // divideCount 계산 최적화
          int divideCount = (int)Math.Ceiling(distance / Tolerance) - 1;
          if (divideCount > 0)
          {
            double quotient = distance / (divideCount + 1);
            customMeshInfo.Add(ele.Key, (startNodeID: nodeA, vector: vector, quotient: quotient, divideCount: divideCount));
          }
        }

        // Bracket 경우 850mm까지는 쪼개지 않고 초과하면 2개로 Mesh 쪼개기
        else if (ele.Value.ExtraData["Group"].Trim().Equals("Bracket"))
        {
          //Console.WriteLine($"{ele.Key}, {ele.Value}");

          double Tolerance = 850.0;
          int nodeA = ele.Value.NodeIDs[0];
          int nodeB = ele.Value.NodeIDs[1];
          double distance = FEModelValidator.DistanceBetweenTwoNodes(nodeA, nodeB, nodeInstance);
          List<double> vector = FEModelValidator.VectorOfTwoNodes(nodeA, nodeB, nodeInstance);

          // divideCount 계산 최적화
          int divideCount = (int)Math.Ceiling(distance / Tolerance) - 1;
          if (divideCount > 0)
          {
            double quotient = distance / (divideCount + 1);
            customMeshInfo.Add(ele.Key, (startNodeID: nodeA, vector: vector, quotient: quotient, divideCount: divideCount));
          }
        }

      }

      // Leg의 경우 KV 자재 연결 포인트 기준 상,하부 2개로 쪼개기
      var legElements = this.elementInstance.Where(ele => ele.Value.ExtraData["Group"].Trim().Equals("Leg")).ToList();

      // 묶을 요소들을 저장할 리스트 : 현재 Leg는 상부, 하부 2개의 Element로 구성되어 있는데 이를 분리하기 위한 과정
      List<List<int>> groupedLegElements = new List<List<int>>();

      // 묶을 Element들을 찾는 로직
      foreach (var ele in legElements)
      {
        bool addedToGroup = false;

        // 기존 그룹 중 나라도 겹치는 Node가 있는지 확인
        foreach (var group in groupedLegElements)
        {
          if (group.Any(node => ele.Value.NodeIDs.Contains(node)))
          {
            group.AddRange(ele.Value.NodeIDs);  // 겹치는 요소가 있으면 해당 그룹에 추가
            addedToGroup = true;
            break;
          }
        }

        // 새로운 그룹으로 추가
        if (!addedToGroup)
        {
          groupedLegElements.Add(new List<int>(ele.Value.NodeIDs));
        }
      }

      // group은 List<int>로 되어 있다고 가정
      for (int i = 0; i < groupedLegElements.Count; i++)
      {
        var group = groupedLegElements[i];
        // 출력: 그룹 번호, 노드 개수, 노드 리스트
        //Console.WriteLine($"[Group {i}] Node Count: {group.Count}, Node IDs: {string.Join(", ", group)}");


        // Distinct하고 Z 값을 기준으로 정렬한 후, group에 덮어씌움
        // 순서는 Leg를 구성하는 Z 기준 제일 낮은 node부터 순서대로 구성        
        groupedLegElements[i] = group.Distinct()
                                     .OrderBy(node => this.nodeInstance[node].Z)
                                     .ToList();
        if (group.Count < 3)
        {
          continue;
        }

        //Console.WriteLine("Grouped Elements (Distinct Sorted Nodes by Z): " + string.Join(", ", groupedLegElements[i]));
        Point3D nodeA = this.nodeInstance[groupedLegElements[i][0]];
        Point3D nodeB = this.nodeInstance[groupedLegElements[i][1]];
        Point3D nodeC = this.nodeInstance[groupedLegElements[i][2]];
        List<double> vectorAB = FEModelValidator.VectorOfTwoNodes(groupedLegElements[i][0], groupedLegElements[i][1], nodeInstance);
        List<double> vectorBC = FEModelValidator.VectorOfTwoNodes(groupedLegElements[i][1], groupedLegElements[i][2], nodeInstance);
        double halfDistanceAB = Math.Round(Math.Sqrt(
            Math.Pow(nodeA.X - nodeB.X, 2) +
            Math.Pow(nodeA.Y - nodeB.Y, 2) +
            Math.Pow(nodeA.Z - nodeB.Z, 2)) / 2, 1);
        double halfDistanceBC = Math.Round(Math.Sqrt(
            Math.Pow(nodeB.X - nodeC.X, 2) +
            Math.Pow(nodeB.Y - nodeC.Y, 2) +
            Math.Pow(nodeB.Z - nodeC.Z, 2)) / 2, 1);


        // 상부 Leg와 하부 Leg의 두 Node 중점 하나를 생성하고 customMeshInfo에 정보 입력
        int nodeID_betweenNodeAB = this.nodeInstance.AddOrGet(
          Math.Round((nodeA.X + nodeB.X) / 2, 1), Math.Round((nodeA.Y + nodeB.Y) / 1), Math.Round((nodeA.Z + nodeB.Z) / 1));
        int matchingAboveElementIDs = this.elementInstance.FindElementByNodeIDs(groupedLegElements[i][0], groupedLegElements[i][1]);
        customMeshInfo.Add(matchingAboveElementIDs, (startNodeID: groupedLegElements[i][0],
          vector: vectorAB, quotient: halfDistanceAB, divideCount: 1));

        int nodeID_betweenNodeBC = this.nodeInstance.AddOrGet(
         Math.Round((nodeB.X + nodeC.X) / 2, 1), Math.Round((nodeB.Y + nodeC.Y) / 1), Math.Round((nodeB.Z + nodeC.Z) / 1));
        int matchingBelowElementIDs = this.elementInstance.FindElementByNodeIDs(groupedLegElements[i][1], groupedLegElements[i][2]);
        customMeshInfo.Add(matchingBelowElementIDs, (startNodeID: groupedLegElements[i][1],
          vector: vectorBC, quotient: halfDistanceBC, divideCount: 1));

        //Console.WriteLine($"{matchingAboveElementIDs}, {matchingAboveElementIDs}");
        //// custom mesh를 쪼갤 정보들을 따로 정리해 둔다. 
        //customMeshInfo.Add(ele.Key, (startNodeID: nodeA, vector: vector, quotient: quotient, divideCount: divideCount));

      }

      //Element 쪼개기 작업 시작
      foreach (var entry in customMeshInfo)
      {
        int eleID = entry.Key;
        int startNodeID = entry.Value.startNodeID;
        List<double> vector = entry.Value.vector;
        double quotient = entry.Value.quotient;
        int divideCount = entry.Value.divideCount;

        List<int> newNodes_list = new List<int>();
        Point3D nodeA_point = this.nodeInstance[startNodeID];

        //Console.WriteLine($"대상 부재:{eleID}");

        for (int i = 0; i < divideCount; i++)
        {
          double newNodeX = nodeA_point.X + (vector[0] * (quotient * (i + 1)));
          double newNodeY = nodeA_point.Y + (vector[1] * (quotient * (i + 1)));
          double newNodeZ = nodeA_point.Z + (vector[2] * (quotient * (i + 1)));

          int newNodeID = this.nodeInstance.AddOrGet(newNodeX, newNodeY, newNodeZ);
          newNodes_list.Add(newNodeID);

          //Console.WriteLine($"quotient:{quotient}, startNode : {startNodeID}, vector:{string.Join(",", vector)}" +
          //  $", nodeID:{newNodeID}, Location:{newNodeX}, {newNodeY}, {newNodeZ}");
        }

        // 짤릴 Node들 정보로 Element 쪼개기 
        List<int> temp = SplitElementByNodes(eleID, newNodes_list);
        newElementList.AddRange(temp);

      } 


      foreach (var entry in customMeshInfo)
      {
        this.elementInstance.Remove(entry.Key);
      }

      Utils.Logger.LogDebug($"[Validator] 커스텀 메쉬 생성(Girder/Column/Bracket/Leg) 완료. {customMeshInfo.Count}개의 부재가 {newElementList.Count}개의 세부 요소로 재구성되었습니다.");
    }




    // 두 Node 사이의 거리를 계산하는 static 메써드
    static double DistanceBetweenTwoNodes(int nodeA, int nodeB, Nodes nodeInstance)
    {
      // nodeInstance에서 각 노드의 좌표(Point3D)를 가져옵니다.
      Point3D pointA = nodeInstance[nodeA];
      Point3D pointB = nodeInstance[nodeB];

      // 각 좌표 간의 차이를 계산합니다.
      double dx = pointB.X - pointA.X;
      double dy = pointB.Y - pointA.Y;
      double dz = pointB.Z - pointA.Z;

      // 유클리드 거리 계산 후 반환합니다.
      return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // 두 node 사이 벡터를 계산하는 static 메써드
    static List<double> VectorOfTwoNodes(int nodeA, int nodeB, Nodes nodeInstance)
    {
      // 노드 A와 B의 좌표를 가져옴
      Point3D pointA = nodeInstance[nodeA];
      Point3D pointB = nodeInstance[nodeB];

      // 두 노드 간의 벡터 성분 계산
      double dx = pointB.X - pointA.X;
      double dy = pointB.Y - pointA.Y;
      double dz = pointB.Z - pointA.Z;

      // 벡터의 크기 계산
      double magnitude = Math.Sqrt(dx * dx + dy * dy + dz * dz);

      // 0인 경우, 정규화 불가능하므로 0 벡터를 반환
      if (magnitude == 0)
      {
        return new List<double> { 0, 0, 0 };
      }

      // 각 성분을 벡터의 크기로 나누어 정규화된 벡터 반환
      return new List<double> { dx / magnitude, dy / magnitude, dz / magnitude };
    }
  }


}
