using System.Collections.Generic;

namespace AAM.LexusRoad.FileTransform.Models
{
    public class LaneConnectsTo
    {
        public string LandId { get; set; }
        public string ConnectingLaneId { get; set; }
        public string ApproachId { get; set; }
        public string ExitId { get; set; }
        public string SignalGroup { get; set; }
        public string Manouver { get; set; }
    }
}
