using UnityEngine;

namespace Mirza.AERO
{
    // This script updates the volumetric fog material with information about
    // the number of additional lights in the scene and ambient lighting.

    [ExecuteAlways]
    public class VolumetricFogController : MonoBehaviour
    {
        public Material material;
        public int additionalLightCountBase;

        void Start()
        {

        }

        void Update()
        {
            Material targetMaterial = ResolveMaterial();
            if (targetMaterial == null)
            {
                return;
            }

            int additionalLightCount = additionalLightCountBase;
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional)
                {
                    additionalLightCount++;
                }
            }

            // Need to loop framecount, else interleaved gradient noise becomes erratic.

            try
            {
                targetMaterial.SetInteger("_FrameCount", Time.renderedFrameCount % 60);
                targetMaterial.SetInteger("_AdditionalLightCount", additionalLightCount);

                Color ambientLighting = RenderSettings.ambientLight * RenderSettings.ambientIntensity;
                targetMaterial.SetColor("_AmbientLighting", ambientLighting);
            }
            catch (MissingReferenceException)
            {
                material = null;
            }
        }

        private Material ResolveMaterial()
        {
            if (material != null)
            {
                return material;
            }

            Renderer renderer = GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = GetComponentInChildren<Renderer>(true);
            }

            if (renderer != null && renderer.sharedMaterial != null)
            {
                material = renderer.sharedMaterial;
            }

            return material;
        }
    }
}
