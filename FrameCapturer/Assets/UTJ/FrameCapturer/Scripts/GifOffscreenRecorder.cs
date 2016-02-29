﻿using System;
using System.Collections;
using UnityEngine;


namespace UTJ
{
    [AddComponentMenu("UTJ/FrameCapturer/GifOffscreenRecorder")]
    [RequireComponent(typeof(Camera))]
    public class GifOffscreenRecorder : IEditableMovieRecorder
    {
        public enum FramerateMode
        {
            Variable,
            Constant,
        }
        [Tooltip("output directory. filename is generated automatically.")]
        public DataPath m_outputDir = new DataPath(DataPath.Root.PersistentDataPath, "");
        public RenderTexture m_target;
        public int m_resolutionWidth = 300;
        public int m_numColors = 256;
        public FramerateMode m_framerateMode = FramerateMode.Variable;
        [Tooltip("relevant only if Framerate Mode is Constant")]
        public int m_framerate = 30;
        public int m_captureEveryNthFrame = 2;
        public int m_keyframe = 30;
        [Tooltip("0 is treated as processor count")]
        public int m_maxTasks = 0;
        public Shader m_sh_copy;

        string m_output_file;
        fcAPI.fcGIFContext m_gif;
        Material m_mat_copy;
        Mesh m_quad;
        RenderTexture m_scratch_buffer;
        int m_num_frames;
        bool m_recode = false;

        public override bool record
        {
            get { return m_recode; }
            set { m_recode = value; }
        }

        public override string GetOutputPath()
        {
            return m_outputDir.GetPath() + "/" + m_output_file;
        }

        public override bool FlushFile()
        {
            return FlushFile(0, -1);
        }

        public override bool FlushFile(int begin_frame, int end_frame)
        {
            bool ret = false;
            if (m_gif.ptr != IntPtr.Zero)
            {
                m_output_file = DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".gif";
                var path = GetOutputPath();
                ret = fcAPI.fcGifWriteFile(m_gif, path, begin_frame, end_frame);
                Debug.Log("GifCapturer.FlushFile(" + begin_frame + ", " + end_frame + "): " + path);
            }
            return ret;
        }

        public override RenderTexture GetScratchBuffer() { return m_scratch_buffer; }

        public override void ResetRecordingState()
        {
            fcAPI.fcGifDestroyContext(m_gif);
            m_gif.ptr = IntPtr.Zero;
            if (m_scratch_buffer != null)
            {
                m_scratch_buffer.Release();
                m_scratch_buffer = null;
            }

            int capture_width = m_resolutionWidth;
            int capture_height = (int)(m_resolutionWidth / ((float)m_target.width / (float)m_target.height));
            m_scratch_buffer = new RenderTexture(capture_width, capture_height, 0, RenderTextureFormat.ARGB32);
            m_scratch_buffer.wrapMode = TextureWrapMode.Repeat;
            m_scratch_buffer.Create();

            m_num_frames = 0;
            if (m_maxTasks <= 0)
            {
                m_maxTasks = SystemInfo.processorCount;
            }
            fcAPI.fcGifConfig conf;
            conf.width = m_scratch_buffer.width;
            conf.height = m_scratch_buffer.height;
            conf.num_colors = m_numColors;
            conf.max_active_tasks = m_maxTasks;
            m_gif = fcAPI.fcGifCreateContext(ref conf);
        }

        public override void EraseFrame(int begin_frame, int end_frame)
        {
            fcAPI.fcGifEraseFrame(m_gif, begin_frame, end_frame);
        }

        public override int GetExpectedFileSize(int begin_frame = 0, int end_frame = -1)
        {
            return fcAPI.fcGifGetExpectedDataSize(m_gif, begin_frame, end_frame);
        }

        public override int GetFrameCount()
        {
            return fcAPI.fcGifGetFrameCount(m_gif);
        }

        public override void GetFrameData(RenderTexture rt, int frame)
        {
            fcAPI.fcGifGetFrameData(m_gif, rt.GetNativeTexturePtr(), frame);
        }

        public fcAPI.fcGIFContext GetGifContext() { return m_gif; }


#if UNITY_EDITOR
        void Reset()
        {
            m_sh_copy = FrameCapturerUtils.GetFrameBufferCopyShader();
        }

        void OnValidate()
        {
            m_numColors = Mathf.Clamp(m_numColors, 1, 255);
        }
#endif // UNITY_EDITOR

        void OnEnable()
        {
            m_quad = FrameCapturerUtils.CreateFullscreenQuad();
            m_mat_copy = new Material(m_sh_copy);

            ResetRecordingState();
        }

        void OnDisable()
        {
            fcAPI.fcGifDestroyContext(m_gif);
            m_gif.ptr = IntPtr.Zero;

            m_scratch_buffer.Release();
            m_scratch_buffer = null;
        }

        IEnumerator OnPostRender()
        {
            if (m_recode)
            {
                yield return new WaitForEndOfFrame();

                if (Time.frameCount % m_captureEveryNthFrame == 0)
                {
                    bool keyframe = m_keyframe > 0 && m_num_frames % m_keyframe == 0;
                    double timestamp = -1.0;
                    if (m_framerateMode == FramerateMode.Constant)
                    {
                        timestamp = 1.0 / m_framerate * m_num_frames;
                    }

                    m_mat_copy.SetTexture("_TmpRenderTarget", m_target);
                    m_mat_copy.SetPass(3);
                    Graphics.SetRenderTarget(m_scratch_buffer);
                    Graphics.DrawMeshNow(m_quad, Matrix4x4.identity);
                    Graphics.SetRenderTarget(null);
                    fcAPI.fcGifAddFrameTexture(m_gif, m_scratch_buffer, keyframe, timestamp);

                    m_num_frames++;
                }
            }
        }
    }

}