using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Model
{
    public interface IPowerEntity
    {
        long Id { get; set; }

        string Name { get; set; }

        double X { get; set; }

        double Y { get; set; }

        List<long> ConnectedTo { get; set; }
    }
}
