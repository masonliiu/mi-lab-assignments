/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oculus.Interaction
{
    /// <summary>
    /// The CollisionsInteractableRegistry maintains a collision map for any Rigidbody-Interactables
    /// pair that utilizes Unity Colliders for overlap checks
    /// </summary>
    public class CollisionInteractionRegistry<TInteractor, TInteractable> :
                             InteractableRegistry<TInteractor, TInteractable>
                             where TInteractor : Interactor<TInteractor, TInteractable>, IRigidbodyRef
                             where TInteractable : Interactable<TInteractor, TInteractable>, IRigidbodyRef
    {
        private Dictionary<Rigidbody, HashSet<TInteractable>> _rigidbodyCollisionMap;
        private Dictionary<Rigidbody, InteractableTriggerBroadcaster> _broadcasters;
        private Dictionary<TInteractable, (Action<Rigidbody>, Action<Rigidbody>)> _handlers;

        public CollisionInteractionRegistry() : base()
        {
            _rigidbodyCollisionMap = new();
            _broadcasters = new();
            _handlers = new();
        }

        public override void Register(TInteractable interactable)
        {
            base.Register(interactable);
            InteractableTriggerBroadcaster broadcaster;
            if (!_broadcasters.TryGetValue(interactable.Rigidbody, out broadcaster))
            {
                GameObject triggerGameObject = interactable.Rigidbody.gameObject;
                broadcaster = triggerGameObject.AddComponent<InteractableTriggerBroadcaster>();
                _broadcasters.Add(interactable.Rigidbody, broadcaster);
            }

            Action<Rigidbody> handleEntered = (rb) => HandleTriggerEntered(interactable, rb);
            Action<Rigidbody> handleExited = (rb) => HandleTriggerExited(interactable, rb);
            _handlers.Add(interactable, (handleEntered, handleExited));
            broadcaster.WhenRigidbodyEntered += handleEntered;
            broadcaster.WhenRigidbodyExited += handleExited;
        }

        public override void Unregister(TInteractable interactable)
        {
            base.Unregister(interactable);

            InteractableTriggerBroadcaster broadcaster;
            if (_broadcasters.TryGetValue(interactable.Rigidbody, out broadcaster))
            {
                var handlers = _handlers[interactable];
                broadcaster.WhenRigidbodyEntered -= handlers.Item1;
                broadcaster.WhenRigidbodyExited -= handlers.Item2;
                _handlers.Remove(interactable);

                if (broadcaster != null
                    && broadcaster.WhenRigidbodyEntered == null
                    && broadcaster.WhenRigidbodyExited == null)
                {
                    _broadcasters.Remove(interactable.Rigidbody);
                    broadcaster.enabled = false;
                    Object.Destroy(broadcaster);
                }
            }
        }

        private void HandleTriggerEntered(TInteractable interactable, Rigidbody rigidbody)
        {
            if (!_rigidbodyCollisionMap.ContainsKey(rigidbody))
            {
                _rigidbodyCollisionMap.Add(rigidbody, new HashSet<TInteractable>());
            }

            HashSet<TInteractable> interactables = _rigidbodyCollisionMap[rigidbody];
            interactables.Add(interactable);
        }

        private void HandleTriggerExited(TInteractable interactable, Rigidbody rigidbody)
        {
            HashSet<TInteractable> interactables = _rigidbodyCollisionMap[rigidbody];
            interactables.Remove(interactable);

            if (interactables.Count == 0)
            {
                _rigidbodyCollisionMap.Remove(rigidbody);
            }
        }

        public override InteractableSet List(TInteractor interactor)
        {
            HashSet<TInteractable> colliding;
            if (_rigidbodyCollisionMap.TryGetValue(interactor.Rigidbody, out colliding))
            {
                return List(interactor, colliding);
            }
            return _empty;
        }

        private static readonly InteractableSet _empty = new InteractableSet();
    }
}
