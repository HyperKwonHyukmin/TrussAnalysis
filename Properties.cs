using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TrussModelBuilder.Model
{
  // 속성 정의 (유형 및 치수)
  public struct PropertyAttribute
  {
    public string Type { get; }
    public IReadOnlyDictionary<string, double> Dim { get; }
    public int MaterialID { get; }

    public PropertyAttribute(string type, Dictionary<string, double> dim, int materialID)
    {
      Type = type;
      Dim = new Dictionary<string, double>(dim); // 불변성 유지
      MaterialID = materialID;
    }

    public override string ToString()
    {
      return $"Type: {Type}, Dimensions: [{string.Join(", ", Dim.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}], MaterialID: {MaterialID}";
    }
  }

  public class Properties : IEnumerable<KeyValuePair<int, PropertyAttribute>> // IEnumerable 구현 (foreach 지원)
  {
    public Materials Material { get; private set; }
    private int propertyID = 0;
    private Dictionary<int, PropertyAttribute> properties = new Dictionary<int, PropertyAttribute>();
    private Dictionary<string, int> propertyLookup = new Dictionary<string, int>();

    public Properties(Materials material)
    {
      Material = material;
    }

    // 🔹 인덱서 추가 (property_cls[propertyID] 형태로 속성 조회 가능)
    public PropertyAttribute this[int propertyID]
    {
      get
      {
        if (!properties.TryGetValue(propertyID, out PropertyAttribute property))
        {
          throw new KeyNotFoundException($"Property ID {propertyID} does not exist.");
        }
        return property;
      }
    }

    // 속성 추가 (중복 방지: 동일한 Type + Dim + MaterialID가 존재하면 기존 ID 반환)
    public int AddOrGet(string Type, Dictionary<string, double> Dim, int materialID)
    {
      string key = $"{Type}|{string.Join(";", Dim.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))}|{materialID}";

      if (propertyLookup.TryGetValue(key, out int existingPropertyID))
      {
        return existingPropertyID; // 기존 속성 ID 반환
      }

      propertyID++;
      PropertyAttribute newProperty = new PropertyAttribute(Type, Dim, materialID);
      properties[propertyID] = newProperty;
      propertyLookup[key] = propertyID;

      return propertyID;
    }

    // 속성 제거 (존재하지 않으면 예외 발생)
    public void Remove(int inputPropertyID)
    {
      if (!properties.TryGetValue(inputPropertyID, out PropertyAttribute removedProperty))
      {
        throw new KeyNotFoundException($"Property ID {inputPropertyID} does not exist.");
      }

      string key = $"{removedProperty.Type}|{string.Join(";", removedProperty.Dim.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"))}|{removedProperty.MaterialID}";
      properties.Remove(inputPropertyID);
      propertyLookup.Remove(key);
    }

    // 마지막 추가된 속성의 ID 반환
    public int GetLastID()
    {
      return propertyID > 0 ? propertyID : -1;
    }

    // 속성 개수 반환
    public int GetCount()
    {
      return properties.Count;
    }

    // 특정 Property ID의 속성 반환
    public PropertyAttribute GetProperty(int inputPropertyID)
    {
      if (!properties.TryGetValue(inputPropertyID, out PropertyAttribute property))
      {
        throw new KeyNotFoundException($"Property with ID {inputPropertyID} does not exist. Available IDs: {string.Join(", ", properties.Keys)}");
      }
      return property;
    }

    public double GetMaxDimension(int propertyID)
    {
      if (!properties.ContainsKey(propertyID))
        throw new KeyNotFoundException($"Property ID {propertyID} does not exist.");

      return properties[propertyID].Dim.Values.Max(); // 가장 큰 치수 반환
    }



    // 🔹 IEnumerable<KeyValuePair<int, PropertyAttribute>> 구현 → propertyID와 함께 속성 정보 확인 가능
    public IEnumerator<KeyValuePair<int, PropertyAttribute>> GetEnumerator()
    {
      return properties.GetEnumerator(); // Dictionary<int, PropertyAttribute>의 KeyValuePair 반환
    }

    // 🔹 IEnumerable 인터페이스 구현 (비제네릭 버전)
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
