using System;
using System.Collections;
using System.Collections.Generic;

/*
     * Materials 클래스 설명서
     * ------------------------
     * 이 클래스는 유한요소 해석을 위한 재료(Material) 속성을 관리하며, 재료 추가, 검색, 제거 등의 기능을 제공합니다.
     *
     * - AddOrGet(double E, double Nu, double Rho): 동일한 물성치를 가진 재료가 존재하면 기존 ID를 반환하고, 없으면 새 재료를 추가 후 ID 반환.
     * - Remove(int inputMaterialID): 특정 재료를 제거 (존재하지 않으면 예외 발생).
     * - GetLastID(): 마지막 추가된 재료의 ID 반환 (재료가 없으면 예외 발생).
     * - GetCount(): 현재 저장된 재료의 개수를 반환.
     * - GetMaterial(int inputMaterialID): 특정 재료의 속성을 반환 (E, Nu, Rho 포함).
     * ------------------------
     */


namespace TrussModelBuilder.Model
{
  // 재료 속성 정의 (탄성계수, 포아송비, 밀도)
  public struct MaterialAttribute
  {
    public double E { get; private set; } // 탄성계수
    public double Nu { get; private set; } // 포아송비
    public double Rho { get; private set; } // 밀도

    public MaterialAttribute(double e, double nu, double rho)
    {
      E = e;
      Nu = nu;
      Rho = rho;
    }

    public override string ToString()
    {
      return $"E: {E}, Nu: {Nu}, Rho: {Rho}";
    }
  }

  public class Materials : IEnumerable<KeyValuePair<int, MaterialAttribute>>
  {
    private int materialID = 0; // 재료 ID 카운터
    private Dictionary<int, MaterialAttribute> materials = new Dictionary<int, MaterialAttribute>(); // 재료 저장
    private Dictionary<string, int> materialLookup = new Dictionary<string, int>(); // 중복 방지용

    // 인덱서 구현 (Material ID를 사용하여 재료 속성에 접근)
    public MaterialAttribute this[int id]
    {
      get
      {
        if (!materials.ContainsKey(id))
        {
          throw new KeyNotFoundException($"Material ID {id} does not exist.");
        }
        return materials[id];
      }
    }

    // 재료 추가 (중복 방지: 동일한 속성이 존재하면 기존 ID 반환)
    public int AddOrGet(double E, double Nu, double Rho)
    {
      string key = $"{E},{Nu},{Rho}";

      if (materialLookup.TryGetValue(key, out int existingMaterialID))
      {
        return existingMaterialID; // 기존 재료 ID 반환
      }

      materialID += 1;
      MaterialAttribute newMaterial = new MaterialAttribute(E, Nu, Rho);
      materials[materialID] = newMaterial;
      materialLookup[key] = materialID;
      return materialID;
    }

    // 특정 재료 제거 (존재하지 않으면 예외 발생)
    public void Remove(int inputMaterialID)
    {
      if (!materials.ContainsKey(inputMaterialID))
      {
        throw new KeyNotFoundException($"Material ID {inputMaterialID} does not exist.");
      }

      MaterialAttribute removedMaterial = materials[inputMaterialID];
      string key = $"{removedMaterial.E},{removedMaterial.Nu},{removedMaterial.Rho}";
      materials.Remove(inputMaterialID);
      materialLookup.Remove(key);
    }

    // 마지막 추가된 재료의 ID 반환
    public int GetLastID()
    {
      return materialID > 0 ? materialID : throw new InvalidOperationException("No materials exist.");
    }

    // 전체 재료 개수 반환
    public int GetCount()
    {
      return materials.Count;
    }

    // 특정 Material ID의 속성 반환
    public MaterialAttribute GetMaterial(int inputMaterialID)
    {
      if (!materials.ContainsKey(inputMaterialID))
      {
        throw new KeyNotFoundException($"Material ID {inputMaterialID} does not exist.");
      }
      return materials[inputMaterialID];
    }

    // IEnumerable<KeyValuePair<int, MaterialAttribute>> 구현 (foreach 지원)
    public IEnumerator<KeyValuePair<int, MaterialAttribute>> GetEnumerator()
    {
      return materials.GetEnumerator(); // Dictionary<int, MaterialAttribute>의 KeyValuePair 반환
    }

    // 비제네릭 IEnumerable 구현
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
