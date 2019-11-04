using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AAM.LexusRoad.FileTransform.Models
{
    public class LaneNode
    {
        public string LaneId { get; set; }
        public int LaneNodeId { get; set; }
        public double DeltaX { get; set; }
        public double DeltaY { get; set; }
        public double DeltaWidth { get; set; }
        public double DeltaElevation { get; set; }
        public string East { get; set; }
        public string North { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public static LaneNode FromCsv(string csvLine)
        {
            string[] values = csvLine.Split(',');
            
            return null;
        }
    }
}
