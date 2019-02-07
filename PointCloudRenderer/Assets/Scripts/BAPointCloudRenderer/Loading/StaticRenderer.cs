﻿using System.Collections.Generic;
using BAPointCloudRenderer.CloudData;
using BAPointCloudRenderer.ObjectCreation;
using System.Threading;
using System;

namespace BAPointCloudRenderer.Loading {
    class StaticRenderer : AbstractRenderer {

        private Queue<Node> toLoad;
        private List<Node> rootNodes;
        private Dictionary<Node, uint> nodePointcounts;
        private Queue<Node> toDisplay;
        private Queue<Node> toRemove;
        private MeshConfiguration config;
        private bool running = false;
        private Thread loadingThread;
        private uint pointcount = 0;
        private bool visible = true;

        public StaticRenderer(MeshConfiguration config) {
            rootNodes = new List<Node>();
            toLoad = new Queue<Node>();
            toDisplay = new Queue<Node>();
            toRemove = new Queue<Node>();
            nodePointcounts = new Dictionary<Node, uint>();
            this.config = config;
        }

        public void AddRootNode(Node rootNode) {
            toLoad.Enqueue(rootNode);
            if (!running) {
                if (loadingThread != null) {
                    loadingThread.Join();
                }
                loadingThread = new Thread(Load);
                loadingThread.Start();
            }
        }

        public uint GetPointCount() {
            return pointcount;
        }

        public int GetRootNodeCount() {
            return rootNodes.Count;
        }

        public void RemoveRootNode(Node rootNode) {
            rootNodes.Remove(rootNode);
            toRemove.Enqueue(rootNode);
        }

        public void ShutDown() {
            running = false;
            if (loadingThread != null) {
                loadingThread.Join();
            }
            toDisplay.Clear();
            toRemove.Clear();
            lock (toRemove) {
                foreach (Node node in rootNodes) {
                    toRemove.Enqueue(node);
                }
            }
            running = true;
            Update();
            running = false;
            rootNodes.Clear();
        }

        public void Update() {
            if (!running) return;
            if (visible) {
                Monitor.Enter(toDisplay);
                while (toDisplay.Count != 0) {
                    Node n = toDisplay.Dequeue();
                    Monitor.Exit(toDisplay);
                    n.CreateAllGameObjects(config);
                    lock (nodePointcounts) {
                        pointcount += nodePointcounts[n];
                    }
                    Monitor.Enter(toDisplay);
                }
                Monitor.Exit(toDisplay);
            }
            Monitor.Enter(toRemove);
            while (toRemove.Count != 0) {
                Node n = toRemove.Dequeue();
                Monitor.Exit(toRemove);
                n.RemoveAllGameObjects(config);
                lock (nodePointcounts) {
                    if (nodePointcounts.ContainsKey(n)) {
                        pointcount -= nodePointcounts[n];
                        nodePointcounts.Remove(n);
                    }
                }
                Monitor.Enter(toRemove);
            }
            Monitor.Exit(toRemove);
        }

        private void Load() {
            running = true;
            while (running) {
                Monitor.Enter(toLoad);
                if (toLoad.Count != 0) {
                    Node n = toLoad.Dequeue();
                    Monitor.Exit(toLoad);
                    uint pc = CloudLoader.LoadAllPointsForNode(n);
                    lock (nodePointcounts) {
                        nodePointcounts.Add(n, pc);
                    }
                    lock (rootNodes) {
                        rootNodes.Add(n);
                    }
                    lock (toDisplay) {
                        toDisplay.Enqueue(n);
                    }
                } else {
                    Monitor.Exit(toLoad);
                }
            }
        }
        

        public void Hide() {
            visible = false;
            lock (rootNodes) {
                foreach (Node n in rootNodes) {
                    n.DeactivateAllGameObjects();
                }
            }
        }

        public void Display() {
            lock (rootNodes) {
                foreach (Node n in rootNodes) {
                    n.ReactivateAllGameObjects();
                }
            }
            visible = true;
        }
    }
}