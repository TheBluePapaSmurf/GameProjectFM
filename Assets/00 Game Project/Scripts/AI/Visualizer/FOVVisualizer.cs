using UnityEngine;

namespace GameProjectFM.AI.Visual
{
    using Core;
    using Systems;

    public class FOVVisualizer : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private VisualSettings visualSettings;
        [SerializeField] private FOVSystem fovSystem;

        // Mesh components for runtime visualization
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh fovMesh;

        public void Initialize(VisualSettings visual, FOVSystem fov)
        {
            visualSettings = visual;
            fovSystem = fov;

            if (visualSettings.showFieldOfView)
            {
                SetupMeshComponents();
            }
        }

        void Update()
        {
            if (visualSettings.showFieldOfView && fovSystem != null)
            {
                UpdateFOVMesh();
            }
        }

        private void SetupMeshComponents()
        {
            // Create child object for FOV visualization
            GameObject fovVisualChild = new GameObject("FOV Visualization");
            fovVisualChild.transform.SetParent(transform);
            fovVisualChild.transform.localPosition = Vector3.zero;

            // Add mesh components
            meshFilter = fovVisualChild.AddComponent<MeshFilter>();
            meshRenderer = fovVisualChild.AddComponent<MeshRenderer>();

            // Create material for FOV
            Material fovMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            fovMaterial.color = visualSettings.fovColor;
            fovMaterial.SetFloat("_Surface", 1); // Transparent
            meshRenderer.material = fovMaterial;

            // Create mesh
            fovMesh = new Mesh();
            fovMesh.name = "FOV Mesh";
            meshFilter.mesh = fovMesh;
        }

        private void UpdateFOVMesh()
        {
            if (fovMesh == null || fovSystem.FOVPoints.Count == 0) return;

            fovMesh.Clear();

            // Create vertices
            Vector3[] vertices = new Vector3[fovSystem.FOVPoints.Count + 1];
            vertices[0] = Vector3.zero; // Center point

            for (int i = 0; i < fovSystem.FOVPoints.Count; i++)
            {
                vertices[i + 1] = transform.InverseTransformPoint(fovSystem.FOVPoints[i]);
            }

            // Create triangles
            int[] triangles = new int[(fovSystem.FOVPoints.Count - 1) * 3];
            for (int i = 0; i < fovSystem.FOVPoints.Count - 1; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            fovMesh.vertices = vertices;
            fovMesh.triangles = triangles;
            fovMesh.RecalculateNormals();
        }

        void OnDrawGizmos()
        {
            if (!visualSettings.showFovInEditor || fovSystem == null) return;

            // Draw FOV in editor
            Gizmos.color = visualSettings.fovEditorColor;

            if (fovSystem.FOVPoints.Count > 1)
            {
                for (int i = 0; i < fovSystem.FOVPoints.Count - 1; i++)
                {
                    Vector3[] triangle = { transform.position, fovSystem.FOVPoints[i], fovSystem.FOVPoints[i + 1] };
                    DrawTriangleGizmo(triangle);
                }
            }

            // Draw FOV border
            Gizmos.color = visualSettings.fovBorderColor;
            if (fovSystem.FOVPoints.Count > 0)
            {
                for (int i = 0; i < fovSystem.FOVPoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(fovSystem.FOVPoints[i], fovSystem.FOVPoints[i + 1]);
                }

                // Lines from center to FOV points
                foreach (Vector3 point in fovSystem.FOVPoints)
                {
                    Gizmos.DrawLine(transform.position, point);
                }
            }
        }

        private void DrawTriangleGizmo(Vector3[] points)
        {
            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[1], points[2]);
            Gizmos.DrawLine(points[2], points[0]);
        }

        void OnDestroy()
        {
            if (fovMesh != null)
            {
                DestroyImmediate(fovMesh);
            }
        }
    }
}
