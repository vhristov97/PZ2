using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Model
{
    public class SubstationEntity : IPowerEntity
    {
        public SubstationEntity()
        {
            ConnectedTo = new List<long>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<long> ConnectedTo { get; set; }
    }
}
