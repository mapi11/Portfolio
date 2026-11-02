using UnityEngine;

namespace Portfolio.Diagnostics
{
    public class FrameStats
    {
        readonly float[] m_Samples;
        readonly float[] m_Sorted;
        int m_Next;
        int m_Count;

        public FrameStats(int window)
        {
            m_Samples = new float[window];
            m_Sorted = new float[window];
        }

        public void Add(float deltaTime)
        {
            m_Samples[m_Next] = deltaTime;
            m_Next = (m_Next + 1) % m_Samples.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Samples.Length);
        }

        public void Reset() => m_Count = m_Next = 0;

        public int Count => m_Count;

        public float AverageMs
        {
            get
            {
                if (m_Count == 0)
                    return 0f;
                var sum = 0f;
                for (var i = 0; i < m_Count; i++)
                    sum += m_Samples[i];
                return sum / m_Count * 1000f;
            }
        }

        public float OnePercentLowFps
        {
            get
            {
                if (m_Count == 0)
                    return 0f;
                System.Array.Copy(m_Samples, m_Sorted, m_Count);
                System.Array.Sort(m_Sorted, 0, m_Count);
                var worst = Mathf.Max(1, m_Count / 100);
                var sum = 0f;
                for (var i = m_Count - worst; i < m_Count; i++)
                    sum += m_Sorted[i];
                return worst / sum;
            }
        }
    }
}
