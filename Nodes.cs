using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;

/*
 * Nodes 클래스 설명서
 * ------------------------
 * 이 클래스는 3D 공간상의 노드(Point3D)를 관리하며, 노드 추가, 검색, 변환 등의 기능을 제공합니다.
 *
 * - AddOrGet(double X, double Y, double Z): 해당 좌표의 노드가 존재하면 기존 ID를 반환하고, 없으면 새 노드를 추가하여 ID 반환.
 * - Remove(int nodeID): 특정 노드를 제거.
 * - FindNodeID(double X, double Y, double Z): 특정 좌표의 Node ID를 반환. 없으면 -1 반환.
 * - GetNodeCoordinates(int nodeID): 특정 Node ID의 좌표를 반환.
 * - GetAllNodes(): 모든 노드 리스트를 반환.
 * - GetNodeCount(): 현재 저장된 노드 개수를 반환.
 * - FindNodesInRange(double X, double Y, double Z, double radius): 특정 좌표 기준으로 일정 거리 내의 노드들을 찾음.
 * - FindNearestNode(double X, double Y, double Z): 특정 좌와 가장 가까운 노드 ID를 반환.
 * - TranslateNode(int nodeID, double dx, double dy, double dz): 특정 노드를 (dx, dy, dz) 만큼 이동.
 * - TranslateAllNodes(double dx, double dy, double dz): 모든 노드를 (dx, dy, dz) 만큼 평행 이동.
 * - MoveNodeTo(int nodeID, double newX, double newY, double newZ): 특정 노드를 새로운 좌표로 이동.
 * - GetDistanceBetweenNodes(int firstNodeID, int secondNodeID): 두 노드 간의 거리 반환.
 * - IEnumerable<KeyValuePair<int, Point3D>> 구현: 노드 컬렉션을 반복문에서 사용할 수 있도록 지원.
 * ------------------------
 */

namespace TrussModelBuilder.Model
{
  public struct Point3D
  {
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public Point3D(double x, double y, double z)
    {
      X = x;
      Y = y;
      Z = z;
    }

    public override string ToString()
    {
      return $"(X:{X}, Y:{Y}, Z:{Z})";
    }
  }

  public class Nodes : IEnumerable<KeyValuePair<int, Point3D>>
  {
    public int nodeID = 0;
    private Dictionary<int, Point3D> nodes = new Dictionary<int, Point3D>();
    private Dictionary<string, int> nodeLookup = new Dictionary<string, int>();

    public int AddOrGet(double X, double Y, double Z)
    {
      string key = X + "," + Y + "," + Z;
      if (nodeLookup.TryGetValue(key, out int existingNodeID))
      {
        return existingNodeID;
      }

      nodeID += 1;
      nodes[nodeID] = new Point3D(X, Y, Z);
      nodeLookup[key] = nodeID;
      return nodeID;
    }

    public void AddWithID(int NodeID, double X, double Y, double Z)
    {
      {
        string key = X + "," + Y + "," + Z;      
        
        nodes[NodeID] = new Point3D(X, Y, Z);
        nodeLookup[key] = NodeID;
        nodeID = NodeID;


      }
    }

    public void Remove(int inputNodeID)
    {
      if (!nodes.ContainsKey(inputNodeID))
      {
        throw new KeyNotFoundException($"Node ID {inputNodeID} does not exist.");
      }

      Point3D removedNode = nodes[inputNodeID];
      nodes.Remove(inputNodeID);
      nodeLookup.Remove(removedNode.X + "," + removedNode.Y + "," + removedNode.Z);
    }

    public int FindNodeID(double X, double Y, double Z)
    {
      string key = X + "," + Y + "," + Z;
      return nodeLookup.TryGetValue(key, out int nodeID) ? nodeID : -1;
    }

    public Point3D GetNodeCoordinates(int nodeID)
    {
      if (!nodes.ContainsKey(nodeID))
      {
        throw new KeyNotFoundException($"Node ID {nodeID} does not exist.");
      }
      return nodes[nodeID];
    }

    public List<KeyValuePair<int, Point3D>> GetAllNodes()
    {
      return new List<KeyValuePair<int, Point3D>>(nodes);
    }

    public int GetNodeCount()
    {
      return nodes.Count;
    }

    public void TranslateAllNodes(double dx, double dy, double dz)
    {
      Dictionary<int, Point3D> updatedNodes = new Dictionary<int, Point3D>();
      Dictionary<string, int> updatedNodeLookup = new Dictionary<string, int>();

      foreach (var node in nodes)
      {
        Point3D oldPoint = node.Value;
        Point3D newPoint = new Point3D(oldPoint.X + dx, oldPoint.Y + dy, oldPoint.Z + dz);

        updatedNodes[node.Key] = newPoint;
        updatedNodeLookup[$"{newPoint.X},{newPoint.Y},{newPoint.Z}"] = node.Key;
      }

      nodes = updatedNodes;
      nodeLookup = updatedNodeLookup;
    }

    public void MoveNodeTo(int nodeID, double newX, double newY, double newZ)
    {
      if (!nodes.ContainsKey(nodeID))
      {
        throw new KeyNotFoundException($"Node ID {nodeID} does not exist.");
      }

      Point3D oldPoint = nodes[nodeID];
      nodeLookup.Remove($"{oldPoint.X},{oldPoint.Y},{oldPoint.Z}");

      Point3D newPoint = new Point3D(newX, newY, newZ);
      nodes[nodeID] = newPoint;
      nodeLookup[$"{newX},{newY},{newZ}"] = nodeID;
    }

    public double GetDistanceBetweenNodes(int firstNodeID, int secondNodeID)
    {
      if (!nodes.ContainsKey(firstNodeID) || !nodes.ContainsKey(secondNodeID))
      {
        throw new KeyNotFoundException("One or both Node IDs do not exist.");
      }

      Point3D firstNode = nodes[firstNodeID];
      Point3D secondNode = nodes[secondNodeID];

      return Math.Sqrt(
          Math.Pow(secondNode.X - firstNode.X, 2) +
          Math.Pow(secondNode.Y - firstNode.Y, 2) +
          Math.Pow(secondNode.Z - firstNode.Z, 2)
      );
    }

    // 🔹 인덱서 추가 (element_cls[elementID] 형태로 요소 조회 가능)
    public Point3D this[int nodeID]
    {
      get
      {
        if (!nodes.ContainsKey(nodeID))
        {
          throw new KeyNotFoundException($"node ID {nodeID} does not exist.");
        }

        return nodes[nodeID];
      }
    }

    public IEnumerator<KeyValuePair<int, Point3D>> GetEnumerator()
    {
      return nodes.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
