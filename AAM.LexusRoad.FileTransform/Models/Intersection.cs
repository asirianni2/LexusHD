using System.Collections.Generic;

namespace AAM.LexusRoad.FileTransform.Models
{
    public class Intersection
    {
        public string IntersectionNo { get; set; }
        public string Revision { get; set; }
        public string LaneWidth { get; set; }
        public RefPoint RefPoint { get; set; }
        public List<Lane> Lanes { get; set; }
    }
}
