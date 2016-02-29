﻿using System.Collections;
using UnityEngine;


namespace UTJ
{
    [AddComponentMenu("UTJ/FrameCapturer/ExrOffscreenRecorder")]
    [RequireComponent(typeof(Camera))]
    public class ExrOffscreenRecorder : MonoBehaviour
    {
        [System.Serializable]
        public class ChannelData
        {
            public string name;
            public int channel;
        }

        [System.Serializable]
        public class CaptureData
        {
            public RenderTexture target;
            public ChannelData[] channels;
        }

        public CaptureData[] m_targets;

        [Tooltip("output directory. filename is generated automatically.")]
        public DataPath m_outputDir = new DataPath(DataPath.Root.CurrentDirectory, "ExrOutput");
        public string m_outputFilename = "Offscreen";
        public int m_beginFrame = 0;
        public int m_endFrame = 100;
        public int m_maxTasks = 1;
        public Shader m_sh_copy;

        fcAPI.fcEXRContext m_exr;
        int m_frame;
        Material m_mat_copy;
        Mesh m_quad;
        RenderTexture[] m_scratch_buffers;


#if UNITY_EDITOR
        void Reset()
        {
            m_sh_copy = FrameCapturerUtils.GetFrameBufferCopyShader();
        }
#endif // UNITY_EDITOR

        void OnEnable()
        {
            m_outputDir.CreateDirectory();
            m_quad = FrameCapturerUtils.CreateFullscreenQuad();
            m_mat_copy = new Material(m_sh_copy);

            m_scratch_buffers = new RenderTexture[m_targets.Length];
            for (int i = 0; i < m_scratch_buffers.Length; ++i)
            {
                var rt = m_targets[i].target;
                m_scratch_buffers[i] = new RenderTexture(rt.width, rt.height, 0, rt.format);
            }

            fcAPI.fcExrConfig conf;
            conf.max_active_tasks = m_maxTasks;
            m_exr = fcAPI.fcExrCreateContext(ref conf);
        }

        void OnDisable()
        {
            fcAPI.fcExrDestroyContext(m_exr);
        }


        IEnumerator OnPostRender()
        {
            int frame = m_frame++;
            if (frame >= m_beginFrame && frame <= m_endFrame)
            {
                yield return new WaitForEndOfFrame();

                Debug.Log("ExrOffscreenCapturer: frame " + frame);

                var rt = m_targets[0].target;
                string path = m_outputDir.GetPath() + "/" + m_outputFilename + "_" + frame.ToString("0000") + ".exr";

                // 上下反転などを行うため、一度スクラッチバッファに内容を移す
                for (int ti = 0; ti < m_targets.Length; ++ti)
                {
                    var target = m_targets[ti];
                    var scratch = m_scratch_buffers[ti];
                    m_mat_copy.SetTexture("_TmpRenderTarget", target.target);
                    m_mat_copy.SetPass(3);
                    Graphics.SetRenderTarget(scratch);
                    Graphics.DrawMeshNow(m_quad, Matrix4x4.identity);
                    Graphics.SetRenderTarget(null);
                }

                // 描画結果を CPU 側に移してファイル書き出し
                fcAPI.fcExrBeginFrame(m_exr, path, rt.width, rt.height);
                for (int ti = 0; ti < m_targets.Length; ++ti)
                {
                    var target = m_targets[ti];
                    var scratch = m_scratch_buffers[ti];
                    for (int ci = 0; ci < target.channels.Length; ++ci)
                    {
                        var ch = target.channels[ci];
                        AddLayer(scratch, ch.channel, ch.name);
                    }
                }
                fcAPI.fcExrEndFrame(m_exr);
            }
        }
        void AddLayer(RenderTexture rt, int ch, string name)
        {
            fcAPI.fcExrAddLayerTexture(m_exr, rt, ch, name, false);
        }
    }
}
