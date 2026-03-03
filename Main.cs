using TrussModelBuilder.Model;
using TrussModelBuilder.Control;
using System.IO;


namespace TrussModelBuilder.View
{
  class MainApp
  {
    static void Main(string[] args)
    {
      // 자체 실행 용
      //string projectDirectory = @"C:\Coding\Csharp\Projects\TrussModelBuilder";
      //string nodeCsv = Path.Combine(workDirectory,"csv", "NODE.csv");
      //string wayCsv = Path.Combine(workDirectory,"csv", "WAY.csv");
      //string nodeCsv = Path.Combine(workDirectory, "chohyeminCSV", "CTTK02T1_GRID_R0-NODE.csv");
      //string wayCsv = Path.Combine(workDirectory, "chohyeminCSV", "CTTK02T1_GRID_R0-WAY.csv");
      //string BDF_path = @"C:\Coding\Csharp\Projects\TrussModelBuilder\bdf\Truss.bdf";
      //string propertyConvertTxt = Path.Combine(projectDirectory, "Reference", "TrussPropertyConvert.txt");
      //string propertyMaterialReferenceBDF = Path.Combine(projectDirectory, "Reference", "Material_Property_Info.bdf");

      // HiTESS 실행 용
      if (args.Length != 3)
      {
        Console.WriteLine("인자 개수가 잘못되었습니다.");
        return;
      }
      string projectDirectory = args[0];
      string nodeCsv = args[1];
      string wayCsv = args[2];
      // property 변환을 위해 정보들이 요약되어 있는 text 파일
      string propertyConvertTxt = Path.Combine(projectDirectory, "Reference", "TrussPropertyConvert.txt");
      // Truss는 정해져 있는 property와 material이 있으므로 따로 생성하지 않고 기존 정보를 붙여넣기 한다. 
      string propertyMaterialReferenceBDF = Path.Combine(projectDirectory, "Reference", "Material_Property_Info.bdf");

      string bdfSaveDirectory = Path.GetDirectoryName(nodeCsv)!;
      string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
      string bdfName = $"{timestamp}.bdf";
      string BDF_path = Path.Combine(bdfSaveDirectory, bdfName);
      

      // 파일 존재 여부 확인
      if (!File.Exists(nodeCsv) || !File.Exists(wayCsv))
      {
        Console.WriteLine("입력한 CSV 파일을 찾을 수 없습니다.");
        return;
      }

      // FE Model 구축에 필요한 Material, Property, Node, Element 인스턴스 생성
      Materials materialInstance = new Materials();
      Properties propertyInstance = new Properties(materialInstance);
      Nodes nodeInstance = new Nodes();
      Elements elementInstance = new Elements(nodeInstance, propertyInstance);

      // Node.csv와 Way.csv의 파싱을 통한 모델 정보 추출
      CsvParse parse = new CsvParse(nodeCsv, wayCsv,
        materialInstance, propertyInstance, nodeInstance, elementInstance, propertyConvertTxt);
      parse.Run();

      // 유한요소 해석 유효성을 검증하는 클래스 
      FEModelValidator validator = new FEModelValidator(
        materialInstance, propertyInstance, nodeInstance, elementInstance);
      validator.Run();


      var bdfBuilder = new BdfBuilder(
      101, materialInstance, propertyInstance, nodeInstance,
      elementInstance, parse.BoundaryCondition_list, propertyMaterialReferenceBDF);

      bdfBuilder.Run();


      // 현재 model 상황을 확인할 수 있는 메써드
      ModelView view = new ModelView(materialInstance, propertyInstance, nodeInstance, elementInstance);
      //view.ViewNodeInstance();
      //view.ViewOneElementInstance(1);
      //view.ViewAllElementInstance();


      File.WriteAllLines(BDF_path, bdfBuilder.BdfLines);
    }
  }
}
