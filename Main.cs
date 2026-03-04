using System.IO;
using TrussModelBuilder.Control;
using TrussModelBuilder.Model;
using TrussModelBuilder.Utils;


namespace TrussModelBuilder.View
{
  class MainApp
  {
    static void Main(string[] args)
    {
      // 자체 실행 용
      string projectDirectory = @"C:\Coding\Csharp\Projects\TrussModelBuilder";
      string nodeCsv = Path.Combine(projectDirectory, "csv", "NODE.csv");
      string wayCsv = Path.Combine(projectDirectory, "csv", "WAY.csv");
      //string nodeCsv = Path.Combine(workDirectory, "chohyeminCSV", "CTTK02T1_GRID_R0-NODE.csv");
      //string wayCsv = Path.Combine(workDirectory, "chohyeminCSV", "CTTK02T1_GRID_R0-WAY.csv");
      //string BDF_path = @"C:\Coding\Csharp\Projects\TrussModelBuilder\bdf\Truss.bdf";
      //string propertyConvertTxt = Path.Combine(projectDirectory, "Reference", "TrussPropertyConvert.txt");
      //string propertyMaterialReferenceBDF = Path.Combine(projectDirectory, "Reference", "Material_Property_Info.bdf");

      try
      {
        if (args.Length != 3)
        {
          Console.WriteLine("인자 개수가 잘못되었습니다.");
          return;
        }
        //string projectDirectory = args[0];
        //string nodeCsv = args[1];
        //string wayCsv = args[2];

        string bdfSaveDirectory = Path.GetDirectoryName(nodeCsv)!;

        // 1. 로거 초기화
        string logDirectory = Path.Combine(bdfSaveDirectory, "Logs");
        Logger.Initialize(logDirectory);

        // 🔹 상세 로그(Debug) 활성화 여부 설정 (테스트 시 true, 운영 시 false)
        Logger.IsDebugMode = true;

        if (Logger.IsDebugMode)
        {
          Logger.LogDebug($"프로젝트 경로: {projectDirectory}");
          Logger.LogDebug($"노드 CSV 경로: {nodeCsv}");
          Logger.LogDebug($"요소 CSV 경로: {wayCsv}");
        }

        string propertyConvertTxt = Path.Combine(projectDirectory, "Reference", "TrussPropertyConvert.txt");
        string propertyMaterialReferenceBDF = Path.Combine(projectDirectory, "Reference", "Material_Property_Info.bdf");

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string bdfName = $"{timestamp}.bdf";
        string BDF_path = Path.Combine(bdfSaveDirectory, bdfName);

        // 파일 존재 여부 확인 및 로깅
        if (!File.Exists(nodeCsv) || !File.Exists(wayCsv))
        {
          Logger.LogError($"입력한 CSV 파일을 찾을 수 없습니다. NodeCsv: {nodeCsv}, WayCsv: {wayCsv}");
          return;
        }

        Logger.LogInfo("FE Model 구축 인스턴스 생성을 시작합니다.");
        Materials materialInstance = new Materials();
        Properties propertyInstance = new Properties(materialInstance);
        Nodes nodeInstance = new Nodes();
        Elements elementInstance = new Elements(nodeInstance, propertyInstance);

        Logger.LogInfo("CSV 파싱을 시작합니다.");
        CsvParse parse = new CsvParse(nodeCsv, wayCsv, materialInstance, propertyInstance, nodeInstance, elementInstance, propertyConvertTxt);
        parse.Run();

        Logger.LogInfo("FE 모델 유효성 검증을 시작합니다.");
        FEModelValidator validator = new FEModelValidator(materialInstance, propertyInstance, nodeInstance, elementInstance);
        validator.Run();

        Logger.LogInfo("BDF 파일 생성을 시작합니다.");
        var bdfBuilder = new BdfBuilder(101, materialInstance, propertyInstance, nodeInstance, elementInstance, parse.BoundaryCondition_list, propertyMaterialReferenceBDF);
        bdfBuilder.Run();

        File.WriteAllLines(BDF_path, bdfBuilder.BdfLines);
        Logger.LogInfo($"해석 파일이 성공적으로 생성되었습니다. 위치: {BDF_path}");
      }
      catch (Exception ex)
      {
        // 전역 에러 처리: 프로세스 중단 전에 에러를 기록
        Logger.LogError("프로그램 실행 중 치명적인 오류가 발생했습니다.", ex);
      }
    }
  }
}
