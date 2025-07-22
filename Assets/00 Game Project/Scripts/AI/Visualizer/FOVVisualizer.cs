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

        [Header("Runtime Material Override")]
        [Tooltip("Override material at runtime. Takes priority over VisualSettings material.")]
        public Material runtimeMaterialOverride;

        // Mesh components for runtime visualization
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh fovMesh;
        private Material currentMaterial;

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
                CheckMaterialUpdate();
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

            // Setup material
            SetupFOVMaterial();

            // Create mesh
            fovMesh = new Mesh();
            fovMesh.name = "FOV Mesh";
            meshFilter.mesh = fovMesh;
        }

        private void SetupFOVMaterial()
        {
            Material materialToUse = GetFOVMaterial();

            if (materialToUse != null)
            {
                // Use custom material
                currentMaterial = new Material(materialToUse);
                meshRenderer.material = currentMaterial;
                Debug.Log($"✅ Using custom FOV material: {materialToUse.name}");
            }
            else
            {
                // Create default material
                currentMaterial = CreateDefaultFOVMaterial();
                meshRenderer.material = currentMaterial;
                Debug.Log("🎨 Using default FOV material with fovColor");
            }
        }

        private Material GetFOVMaterial()
        {
            // Priority: Runtime Override > VisualSettings Custom Material
            if (runtimeMaterialOverride != null)
                return runtimeMaterialOverride;

            if (visualSettings.customFovMaterial != null)
                return visualSettings.customFovMaterial;

            return null;
        }

        private Material CreateDefaultFOVMaterial()
        {
            Material defaultMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            // Apply color from settings
            defaultMaterial.color = visualSettings.fovColor;

            // Configure transparency for URP
            SetupTransparencyProperties(defaultMaterial);

            return defaultMaterial;
        }

        private void SetupTransparencyProperties(Material material)
        {
            // Configure material for transparency
            material.SetFloat("_Surface", 1); // Transparent
            material.SetFloat("_Blend", 0); // Alpha blend
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000; // Transparent queue
            material.enableInstancing = false;
        }

        private void CheckMaterialUpdate()
        {
            Material newMaterial = GetFOVMaterial();

            // Check if material has changed
            bool materialChanged = false;

            if (newMaterial != null && (currentMaterial == null || !AreMaterialsSame(currentMaterial, newMaterial)))
            {
                materialChanged = true;
            }
            else if (newMaterial == null && currentMaterial != null && currentMaterial.shader.name != "Universal Render Pipeline/Unlit")
            {
                materialChanged = true;
            }

            if (materialChanged)
            {
                SetupFOVMaterial();
            }

            // Update color if using default material
            if (newMaterial == null && currentMaterial != null)
            {
                if (currentMaterial.color != visualSettings.fovColor)
                {
                    currentMaterial.color = visualSettings.fovColor;
                }
            }
        }

        private bool AreMaterialsSame(Material mat1, Material mat2)
        {
            if (mat1 == null || mat2 == null) return false;
            return mat1.shader == mat2.shader && mat1.name.Contains(mat2.name);
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

        // Public methods for runtime material control
        public void SetCustomMaterial(Material material)
        {
            runtimeMaterialOverride = material;
            SetupFOVMaterial();
        }

        public void ClearCustomMaterial()
        {
            runtimeMaterialOverride = null;
            SetupFOVMaterial();
        }

        public void SetTransparency(float alpha)
        {
            if (currentMaterial != null)
            {
                Color currentColor = currentMaterial.color;
                currentColor.a = Mathf.Clamp01(alpha);
                currentMaterial.color = currentColor;
            }
        }

        public Material GetCurrentMaterial()
        {
            return currentMaterial;
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

            if (currentMaterial != null)
            {
                DestroyImmediate(currentMaterial);
            }
        }
    }
}
