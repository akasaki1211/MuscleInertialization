using UnityEngine;

namespace AkBoneDynamics
{
    public class AkBDSphereCollider : AkBDCollider
    {
        [SerializeField] private float m_Radius = 0.5f;
        [SerializeField] private bool m_DrawGizmo = true;
        [SerializeField] private bool m_Debug = true;

        public override (Vector3 newCenter, bool isCollide) CollisionDetection(float radius, Vector3 center)
        {
            var isCollide = false;
            var newCenter = center;
            var sumRadius = radius + m_Radius;
            if (sumRadius > (center - transform.position).magnitude)
            {
                isCollide = true;
                newCenter = transform.position + ((center - transform.position).normalized * sumRadius);
                // debug
                if (m_Debug) {Debug.DrawLine(transform.position, newCenter, Color.magenta, 0, true);}
            }

            return (newCenter, isCollide);
        }

        void OnDrawGizmos()
        {
            if (m_DrawGizmo)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Vector3.zero, m_Radius);

                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}