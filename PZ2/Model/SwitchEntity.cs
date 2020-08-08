using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Model
{
    public class SwitchEntity : IPowerEntity
    {
        private string status;

        public SwitchEntity()
        {
            ConnectedTo = new List<long>();
        }

        public string Status
        {
            get
            {
                return status;
            }

            set
            {
                status = value;
            }
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public List<long> ConnectedTo { get; set; }
    }
}
