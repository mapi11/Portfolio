using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Scanner
{
    // Волна сканирования поверх кадра: восстанавливает мировую позицию пикселя из глубины и рисует фронт волны.
    // Проход добавляется, только пока в сцене есть активный ScanPulse, остальные сцены за него не платят
    public class ScanRendererFeature : ScriptableRendererFeature
    {
        public Material material;
        public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        ScanPass m_Pass;

        public override void Create()
        {
            // без промежуточной цели URP может рисовать сразу в бэкбуфер, и проход пропустился бы молча
            m_Pass = new ScanPass { renderPassEvent = passEvent, requiresIntermediateTexture = true };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || !ScanPulse.Visible)
                return;
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game && cameraType != CameraType.SceneView)
                return;

            m_Pass.material = material;
            m_Pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(m_Pass);
        }

        class ScanPass : ScriptableRenderPass
        {
            static readonly int s_InverseViewProjection = Shader.PropertyToID("_ScanInverseViewProjection");

            public Material material;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                // в бэкбуфер читать нельзя: без промежуточной цели проход пропускаем
                if (resources.isActiveTargetBackBuffer)
                    return;

                // в полноэкранном проходе матрицы камеры не выставлены, поэтому мировую позицию из глубины
                // восстанавливаем по своей обратной матрице вида-проекции
                var cameraData = frameData.Get<UniversalCameraData>();
                var projection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), false);
                material.SetMatrix(s_InverseViewProjection, (projection * cameraData.GetViewMatrix()).inverse);

                var source = resources.activeColorTexture;
                var description = renderGraph.GetTextureDesc(source);
                description.name = "_ScanPulseColor";
                description.clearBuffer = false;
                var destination = renderGraph.CreateTexture(description);

                renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0), "Scan Pulse");
                resources.cameraColor = destination;
            }
        }
    }
}
