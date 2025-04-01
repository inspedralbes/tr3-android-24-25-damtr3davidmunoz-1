using UnityEngine;
using System.Collections.Generic;

public static class MainThreadDispatcher
{
    private static readonly Queue<System.Action> executionQueue = new Queue<System.Action>();

    [RuntimeInitializeOnLoadMethod]
    private static void Initialize()
    {
        var dispatcher = new GameObject("MainThreadDispatcher").AddComponent<MonoBehaviourDispatcher>();
        Object.DontDestroyOnLoad(dispatcher.gameObject);
    }

    public static void Run(System.Action action)
    {
        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    private class MonoBehaviourDispatcher : MonoBehaviour
    {
        private void Update()
        {
            lock (executionQueue)
            {
                while (executionQueue.Count > 0)
                {
                    executionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }
}