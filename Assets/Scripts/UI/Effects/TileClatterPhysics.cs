using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KillingMahjong.UI.Effects
{
    /// <summary>卓上の接触・回転・摩擦。他の演出に干渉しない局所物理シーンを所有する。</summary>
    internal sealed class TileClatterPhysics : IDisposable
    {
        private const float Scale = 40f, Clearance = 1.6f, FixedStep = 1f / 240f;
        private static int nextId;
        private static readonly Vector2[] Normals = { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
            new Vector2(1, 1.414f).normalized, new Vector2(-1, 1.414f).normalized };
        private static readonly float Diagonal = (186f + 1.414f * 205) / Scale / Mathf.Sqrt(1 + 1.414f * 1.414f);
        private static readonly float[] Limits = { 390f / Scale, 390f / Scale, 203f / Scale, -12f / Scale, Diagonal, Diagonal };
        private readonly List<RectTransform> visuals;
        private readonly Vector2 origin;
        private readonly Scene scene;
        private readonly PhysicsScene2D physics;
        private readonly PhysicsMaterial2D material;
        private readonly Rigidbody2D[] bodies;
        private readonly BoxCollider2D[] colliders;
        private readonly Vector2[] centers;
        private readonly float[] radii;
        private readonly Rigidbody2D hand;
        private readonly CircleCollider2D handCollider;
        private bool disposed, hasPointer, contactArmed;
        private Vector2 lastPointer;
        private float accumulator;
        public string SceneName => scene.name;

        public TileClatterPhysics(List<RectTransform> tiles, Vector2 tableOrigin, TileClatterEffect.TileArt art, bool scatter)
        {
            visuals = tiles; origin = tableOrigin;
            scene = SceneManager.CreateScene("TileClatterPhysics_" + ++nextId, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            material = new PhysicsMaterial2D("TileClatterFelt") { friction = .58f, bounciness = 0 };
            bodies = new Rigidbody2D[tiles.Count]; colliders = new BoxCollider2D[tiles.Count];
            centers = new Vector2[tiles.Count]; radii = new float[tiles.Count];
            try
            {
                for (int i = 0; i < tiles.Count; i++)
                {
                    var rb = NewObject("ContactTile" + i).AddComponent<Rigidbody2D>();
                    rb.gravityScale = 0; rb.mass = .035f; rb.linearDamping = 8; rb.angularDamping = 9;
                    rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.sleepMode = RigidbodySleepMode2D.StartAwake;
                    var box = rb.gameObject.AddComponent<BoxCollider2D>(); box.sharedMaterial = material;
                    // 全32枚の寝かせた既存画像のalpha外周を実測した寸法。
                    // 本番では画像のRead/WriteやGPU読戻しを必要にしない。
                    float height = art == TileClatterEffect.TileArt.Discard ? 260f : 270f;
                    float bottom = art == TileClatterEffect.TileArt.Discard ? 20f : 10f;
                    centers[i] = new Vector2(5, bottom + height / 2 - 160f) * (42f / 320f);
                    var size = new Vector2(190, height) * (42f / 320f);
                    if (tiles[i].GetComponent<UnityEngine.UI.Image>().sprite == null) { centers[i] = Vector2.zero; size = tiles[i].sizeDelta; }
                    box.size = (size + Vector2.one * Clearance) / Scale;
                    bodies[i] = rb; colliders[i] = box; radii[i] = box.size.magnitude * .5f;
                    rb.rotation = tiles[i].localEulerAngles.z;
                    rb.position = (tiles[i].anchoredPosition + origin + Rotate(centers[i], rb.rotation)) / Scale;
                }
                if (scatter) Scatter();
                var edge = NewObject("TableBoundary").AddComponent<EdgeCollider2D>(); edge.sharedMaterial = material; edge.edgeRadius = .025f;
                edge.points = new[] { new Vector2(-392,10)/Scale, new Vector2(-392,62)/Scale, new Vector2(-186,205)/Scale,
                    new Vector2(186,205)/Scale, new Vector2(392,62)/Scale, new Vector2(392,10)/Scale, new Vector2(-392,10)/Scale };
                hand = NewObject("HandContact").AddComponent<Rigidbody2D>(); hand.bodyType = RigidbodyType2D.Kinematic;
                hand.useFullKinematicContacts = true; hand.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                handCollider = hand.gameObject.AddComponent<CircleCollider2D>(); handCollider.radius = 6f / Scale; handCollider.sharedMaterial = material;
                ResetPointer(); SolveConstraints(16);
            }
            catch { Dispose(); throw; }
        }
        private GameObject NewObject(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        private void Scatter()
        {
            var saved = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(1981);
                bool allPlaced = true;
                for (int i = 0; i < bodies.Length; i++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 10000; attempt++)
                    {
                        var point = UnityEngine.Random.insideUnitCircle; var p = new Vector2(point.x * 280, 112 + point.y * 78);
                        float angle = UnityEngine.Random.Range(-14f, 14f); float half = Mathf.Min(376, 188 + (208 - p.y) * 1.414f - 35);
                        if (Mathf.Abs(p.x) > half) continue;
                        bodies[i].position = p / Scale; bodies[i].rotation = angle; bool clear = true;
                        for (int j = 0; j < i; j++)
                        {
                            var d = colliders[i].Distance(bodies[i].position, angle, colliders[j], bodies[j].position, bodies[j].rotation);
                            if (!d.isValid || d.distance < .04f) { clear = false; break; }
                        }
                        if (!clear) continue; placed = true; break;
                    }
                    if (!placed) { allPlaced = false; break; }
                }
                // 保険札などで散らし切れなくても、重ねたまま表示しない。
                if (!allPlaced) for (int i = 0; i < bodies.Length; i++)
                { bodies[i].position = new Vector2(-195 + (i % 7) * 65, 42 + (i / 7) * 43) / Scale; bodies[i].rotation = 0; }
            }
            finally { UnityEngine.Random.state = saved; }
        }
        public void ResetPointer()
        {
            hasPointer = contactArmed = false;
            if (handCollider != null) handCollider.enabled = false;
            if (hand != null) hand.linearVelocity = Vector2.zero;
        }
        private bool PointerClear(Vector2 point)
        {
            float radius = 6f / Scale + .01f;
            for (int i = 0; i < bodies.Length; i++)
            {
                var local = Rotate(point - bodies[i].position, -bodies[i].rotation); var h = colliders[i].size * .5f;
                var nearest = new Vector2(Mathf.Clamp(local.x, -h.x, h.x), Mathf.Clamp(local.y, -h.y, h.y));
                if ((local - nearest).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }
        public void Step(float deltaTime, Vector2? pointer)
        {
            if (disposed || !physics.IsValid() || deltaTime <= 0) return;
            bool moving = false; Vector2 target = lastPointer;
            if (pointer.HasValue)
            {
                target = pointer.Value / Scale;
                if (!hasPointer || Vector2.Distance(target, lastPointer) > 5f)
                {
                    // 再表示や画面外からの座標飛びを、手払いとして扱わない。
                    hand.position = target; lastPointer = target; hasPointer = true; contactArmed = false; handCollider.enabled = false;
                }
                if (!contactArmed && PointerClear(target))
                { contactArmed = true; hand.position = lastPointer = target; handCollider.enabled = true; }
                moving = (target - lastPointer).sqrMagnitude > .0000001f;
            }
            else ResetPointer();
            bool awake = false;
            for (int i = 0; i < bodies.Length; i++) if (!bodies[i].IsSleeping()) { awake = true; break; }
            if (!awake && !moving) { lastPointer = target; accumulator = 0; return; }
            accumulator += Mathf.Min(deltaTime, .05f); int steps = Mathf.FloorToInt((accumulator + .0000001f) / FixedStep);
            for (int s = 0; s < steps; s++)
            {
                if (hasPointer && contactArmed) hand.MovePosition(Vector2.Lerp(lastPointer, target, (s + 1f) / steps));
                physics.Simulate(FixedStep); SolveConstraints(4);
                for (int i = 0; i < bodies.Length; i++)
                {
                    bodies[i].linearVelocity = Vector2.MoveTowards(Vector2.ClampMagnitude(bodies[i].linearVelocity, 20), Vector2.zero, 2.5f * FixedStep);
                    bodies[i].angularVelocity = Mathf.MoveTowards(Mathf.Clamp(bodies[i].angularVelocity, -170, 170), 0, 100 * FixedStep);
                }
                accumulator -= FixedStep;
            }
            // 密集した牌を往復して押すときも、表示前に接触制約を収束させる。
            // 動いていない／収束済みなら1周で抜ける。
            if (steps > 0) SolveConstraints(192);
            lastPointer = target; Render();
        }
        private void SolveConstraints(int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                bool moved = false;
                for (int i = 0; i < bodies.Length; i++) for (int j = i + 1; j < bodies.Length; j++)
                {
                    float radius = radii[i] + radii[j];
                    if ((bodies[i].position - bodies[j].position).sqrMagnitude > radius * radius) continue;
                    var d = colliders[i].Distance(colliders[j]); if (!d.isValid || d.distance >= -.004f) continue;
                    var correction = d.normal * ((-d.distance + .002f) * .5f);
                    bodies[i].position -= correction; bodies[j].position += correction; moved = true;
                    float closing = Vector2.Dot(bodies[j].linearVelocity - bodies[i].linearVelocity, d.normal);
                    if (closing < 0) { bodies[i].linearVelocity += d.normal * (closing * .5f); bodies[j].linearVelocity -= d.normal * (closing * .5f); }
                }
                for (int i = 0; i < bodies.Length; i++)
                {
                    var x = Rotate(Vector2.right, bodies[i].rotation); var y = new Vector2(-x.y, x.x); var half = colliders[i].size * .5f;
                    for (int k = 0; k < Normals.Length; k++)
                    {
                        var n = Normals[k]; float support = Mathf.Abs(Vector2.Dot(n, x)) * half.x + Mathf.Abs(Vector2.Dot(n, y)) * half.y;
                        float excess = Vector2.Dot(bodies[i].position, n) + support - Limits[k]; if (excess <= .000001f) continue;
                        bodies[i].position -= n * excess; float outward = Vector2.Dot(bodies[i].linearVelocity, n);
                        if (outward > 0) bodies[i].linearVelocity -= n * outward; moved = true;
                    }
                }
                if (!moved) break;
            }
        }
        private static Vector2 Rotate(Vector2 value, float degrees)
        { float a = degrees * Mathf.Deg2Rad; return new Vector2(value.x * Mathf.Cos(a) - value.y * Mathf.Sin(a), value.x * Mathf.Sin(a) + value.y * Mathf.Cos(a)); }
        public void Render()
        {
            if (disposed) return;
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                visuals[i].anchoredPosition = bodies[i].position * Scale - origin - Rotate(centers[i], bodies[i].rotation);
                visuals[i].localRotation = Quaternion.Euler(0, 0, bodies[i].rotation);
            }
        }
        public float ReadOpaqueOverlap()
        {
            if (disposed) return 0; float penetration = 0;
            for (int i = 0; i < colliders.Length; i++) for (int j = i + 1; j < colliders.Length; j++)
                penetration = Mathf.Max(penetration, -colliders[i].Distance(colliders[j]).distance * Scale);
            return Mathf.Max(0, penetration - Clearance);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);
                SceneManager.UnloadSceneAsync(scene);
            }
            if (material != null) UnityEngine.Object.Destroy(material);
        }
    }
}
