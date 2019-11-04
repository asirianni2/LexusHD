using System.Collections.Generic;

namespace AAM.LexusRoad.FileTransform.Models
{
    public class Lane
    {
        public string IntersectionNo { get; set; }
        public string ApproachType { get; set; }
        public string LaneId { get; set; }
        public string LaneManouver { get; set; }
        public List<LaneNode> LaneNodes { get; set; }
        public List<LaneConnectsTo> LaneConnections { get; set; }
    }
}
