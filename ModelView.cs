using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrussModelBuilder.Model;

namespace TrussModelBuilder.View
{
  public class ModelView
  {
    public Materials materialInstance;
    public Properties propertyInstance;
    public Nodes nodeInstance;
    public Elements elementInstance;

    public ModelView(Materials materialInstance, Properties propertyInstance,
      Nodes nodeInstance, Elements elementInstance) 
    {
      this.materialInstance = materialInstance;
      this.propertyInstance = propertyInstance;
      this.nodeInstance = nodeInstance;
      this.elementInstance = elementInstance;
    }

    public void ViewNodeInstance()
    {
      foreach(var node in this.nodeInstance) 
      {
        Console.WriteLine(node);
      }
    }

    public void ViewAllElementInstance()
    {
      foreach(var ele in this.elementInstance)
      {
        int elementID = ele.Key;
        ElementAttribute element = ele.Value;
        Console.WriteLine($"{elementID}, {element.PropertyID}, " +
          $"({element.NodeIDs[0]},{element.NodeIDs[1]}, {string.Join(", ",element.LocalAxis)}");        
      }
    }

    public void ViewOneElementInstance(int eleID)
    {
      ElementAttribute value = this.elementInstance[eleID];
      Console.WriteLine($"{eleID}, {string.Join(", ",value.LocalAxis)}");

    }
  }
}
