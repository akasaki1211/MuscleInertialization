using UnityEngine;

namespace AkBoneDynamics
{
    public class AkBDCollider : MonoBehaviour
    {
        public virtual (Vector3 newCenter, bool isCollide) CollisionDetection(float radius, Vector3 center) 
        {
            return (Vector3.zero, true);
        }
    }
}