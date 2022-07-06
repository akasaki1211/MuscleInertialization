using UnityEngine;

namespace AkBoneDynamics
{
    public class AkBDCapsuleCollider : AkBDCollider
    {
        private enum Direction
        {
            XAxis,
            YAxis,
            ZAxis,
        }

        [SerializeField] private float m_RadiusA = 0.5f;
        [SerializeField] private float m_RadiusB = 0.5f;
        [SerializeField] private float m_Height = 1f;
        [SerializeField] private Direction m_Direction = Direction.YAxis;
        [SerializeField] private bool m_DrawGizmo = true;
        [SerializeField] private bool m_Debug = true;

        public override (Vector3 newCenter, bool isCollide) CollisionDetection(float radius, Vector3 center)
        {
            // Get capsule matrix
            var capsuleMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Center of two spheres
            var tipA = Vector3.zero;
            tipA[(int)m_Direction] = m_Height / 2f;
            var tipB = -tipA;
            tipA = capsuleMatrix.MultiplyPoint3x4(tipA);
            tipB = capsuleMatrix.MultiplyPoint3x4(tipB);

            // Get project point
            var vecAB = (tipB - tipA).normalized;
            var nearLength = Vector3.Dot(vecAB, (center - tipA));
            var nearPos = tipA + (vecAB * nearLength);

            // Detection
            var lengthRatio = nearLength / (tipB - tipA).magnitude;
            var isCollide = false;
            var newCenter = center;
            var sumRadius = 0f;
            if (lengthRatio > 1)
            {
                sumRadius = radius + m_RadiusB;
                if (sumRadius > (center - tipB).magnitude)
                {
                    isCollide = true;
                    newCenter = tipB + ((center - tipB).normalized * sumRadius);
                    // debug
                    if (m_Debug) { Debug.DrawLine(tipB, newCenter, Color.magenta, 0, true); }
                }
            }
            else if (lengthRatio < 0)
            {
                sumRadius = radius + m_RadiusA;
                if (sumRadius > (center - tipA).magnitude)
                {
                    isCollide = true;
                    newCenter = tipA + ((center - tipA).normalized * sumRadius);
                    // debug
                    if (m_Debug) {Debug.DrawLine(tipA, newCenter, Color.magenta, 0, true);}
                }
            }
            else
            {
                sumRadius = radius + Mathf.Lerp(m_RadiusA, m_RadiusB, lengthRatio);
                if (sumRadius > (center - nearPos).magnitude)
                {
                    isCollide = true;
                    newCenter = nearPos + ((center - nearPos).normalized * sumRadius);
                    // debug
                    if (m_Debug) {Debug.DrawLine(nearPos, newCenter, Color.magenta, 0, true);}
                }
            }

            return (newCenter, isCollide);
        }

        void OnDrawGizmos()
        {
            if (m_DrawGizmo)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.color = Color.yellow;

                var tipPos = Vector3.zero;
                tipPos[(int)m_Direction] = m_Height / 2f;
                var scl = Vector3.one;
                scl[(int)m_Direction] = -1;

                Gizmos.DrawWireSphere(tipPos, m_RadiusA);
                Gizmos.DrawWireSphere(-tipPos, m_RadiusB);

                for (int i = 0; i < 8; i++)
                {
                    float theta = 45f * i * Mathf.Deg2Rad;
                    var tipPos2 = Vector3.Scale(tipPos, scl);
                    tipPos[((int)m_Direction + 1) % 3] = Mathf.Sin(theta) * m_RadiusA;
                    tipPos[((int)m_Direction + 2) % 3] = Mathf.Cos(theta) * m_RadiusA;
                    tipPos2[((int)m_Direction + 1) % 3] = Mathf.Sin(theta) * m_RadiusB;
                    tipPos2[((int)m_Direction + 2) % 3] = Mathf.Cos(theta) * m_RadiusB;

                    Gizmos.DrawLine(tipPos, tipPos2);
                }

                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}