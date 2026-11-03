using UnityEngine;
using System.Threading;
using Zoo.Content;

namespace Zoo.Simulation
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class AnimalActor : MonoBehaviour
    {
        public int Id { get; private set; }
        public AnimalDefinition Definition { get; private set; }
        public bool IsAlive { get; private set; }
        public Transform VisualRoot { get; private set; }
        Rigidbody body;
        IMovementBehavior movement;
        InteractionResolver interactions;
        Vector3 direction;
        Vector3 targetDirection;
        float previousHeight;
        float currentHeight;
        Quaternion previousRotation;
        Quaternion currentRotation;
        float turnTimer;
        float recoilTime;
        CancellationTokenSource lifetime;
        public CancellationToken LifetimeToken { get; private set; }

        public void Initialize(int id, AnimalDefinition definition, InteractionResolver resolver, PhysicsMaterial material)
        {
            Id = id;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            LifetimeToken = lifetime.Token;
            Definition = definition;
            interactions = resolver;
            movement = MovementFactory.Create(definition.movement);
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var collider = GetComponent<SphereCollider>();
            collider.radius = 0.48f;
            collider.sharedMaterial = material;
            VisualRoot = new GameObject("Visual").transform;
            VisualRoot.SetParent(transform, false);
            ChooseDirection();
            direction = targetDirection;
            previousRotation = currentRotation = Quaternion.LookRotation(direction);
            previousHeight = currentHeight = movement.VisualHeight;
            VisualRoot.localPosition = Vector3.up * currentHeight;
            VisualRoot.rotation = currentRotation;
            IsAlive = true;
        }

        public void Tick(float dt, Rect bounds)
        {
            if (!IsAlive) return;
            var p = body.position;
            if (!bounds.Contains(new Vector2(p.x, p.z)))
            {
                targetDirection = new Vector3(bounds.center.x - p.x, 0, bounds.center.y - p.z).normalized;
                turnTimer = 2;
            }
            else if ((turnTimer -= dt) <= 0) ChooseDirection();
            direction = Vector3.RotateTowards(direction, targetDirection, Mathf.PI * dt, 0);
            previousHeight = currentHeight;
            previousRotation = currentRotation;
            var speed = movement.Speed(dt);
            if (recoilTime > 0) recoilTime -= dt;
            else body.linearVelocity = direction * speed;
            currentHeight = movement.VisualHeight;
            currentRotation = Quaternion.LookRotation(direction);
        }

        void LateUpdate()
        {
            if (!IsAlive) return;
            // Match Rigidbody interpolation: render between the previous and current physics poses.
            // Never write the body's transform here; PhysX remains authoritative for contacts.
            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            VisualRoot.localPosition = Vector3.up * Mathf.Lerp(previousHeight, currentHeight, alpha);
            VisualRoot.rotation = Quaternion.Slerp(previousRotation, currentRotation, alpha);
        }

        void ChooseDirection()
        {
            var angle = Random.value * Mathf.PI * 2;
            targetDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            turnTimer = Random.Range(2f, 5f);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.TryGetComponent<AnimalActor>(out var other)) return;
            recoilTime = 0.3f; // Let the contact impulse separate bodies before the motor takes over.
            interactions.Enqueue(this, other);
        }

        public bool TryDie()
        {
            if (!IsAlive) return false;
            IsAlive = false;
            lifetime.Cancel();
            GetComponent<Collider>().enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}
