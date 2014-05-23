//http://damieng.com/blog/2008/04/10/using-linq-to-foreach-over-an-enum-in-c
 using System.Collections.Generic;
 using System.Linq;

namespace Excavator.CSV
{
   
    public static class Enums
    {
        public static IEnumerable<T> Get<T>()
        {
            return System.Enum.GetValues(typeof(T)).Cast<T>();
        }
    }
}
