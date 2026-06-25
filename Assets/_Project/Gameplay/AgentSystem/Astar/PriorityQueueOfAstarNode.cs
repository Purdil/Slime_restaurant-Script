using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace _Project.Gameplay.AgentSystem.Astar
{
    public struct PriorityQueueOfAstarNode
    {
        public NativeList<AstarNode> heap;
        public int Count => heap.Length;


        public PriorityQueueOfAstarNode(Allocator allocator)
        {
            heap = new NativeList<AstarNode>(allocator);
        }

        public bool TryGetIndex(Vector2Int findNode, out int findIndex )
        {
            for (int i = 0; i < Count; i++)
            {
                if (heap[i].cellPosition == findNode)
                {
                    findIndex = i;
                    return true;
                }
            }
            findIndex = -1;
            return false;
        }

        public void Push(AstarNode data)
        {
            heap.Add(data);
            int now = heap.Length - 1;

            while (now > 0)
            {
                int next = (now - 1) / 2;
                if (heap[now].CompareTo(heap[next]) < 0)
                {
                    break;
                }
                
                (heap[now], heap[next]) = (heap[next], heap[now]);
                now = next;
            }
        }

        public AstarNode Pop()
        {
            AstarNode ret = heap[0];

            int lastIndex = heap.Length - 1;
            heap[0] = heap[lastIndex]; 
            heap.RemoveAt(lastIndex); //마지막을 지운다.
            lastIndex--;

            int now = 0;
            while (true)
            {
                int left = 2 * now + 1;
                int right = 2 * now + 2;
                int next = now;

                if (left <= lastIndex && heap[next].CompareTo(heap[left]) < 0)
                {
                    next = left;
                }

                if (right <= lastIndex && heap[next].CompareTo(heap[right]) < 0)
                {
                    next = right;
                }

                if (next == now)
                {
                    break;
                }
                
                (heap[now], heap[next]) = (heap[next], heap[now]);
                now = next;
            }

            return ret;
        }

        public AstarNode Peek()
        {
            return heap.Length == 0 ? default : heap[0];
        }
        
        public void Dispose()
        {
            heap.Dispose();
        }
    }
}
