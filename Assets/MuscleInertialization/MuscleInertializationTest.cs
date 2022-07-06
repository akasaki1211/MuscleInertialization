using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace MuscleInertialization
{
    [RequireComponent(typeof(Animator))]
    public class MuscleInertializationTest : MonoBehaviour
    {
        private abstract class InertializationBase<T>
        {
            // variable
            public T SecondaryCacheValue;
            public T PrimaryCacheValue;
            public float PrimaryToSecondaryDeltaSeconds;

            public float x0;
            public float v0;
            public Vector3 direction = Vector3.zero;

            public float t1;
            public float a0;
            public float A;
            public float B;
            public float C;

            public bool isTransition = false;

            // Initialize x0, v0
            public abstract void Init1(T RequestedValue, float transitionDuration);

            // Initialize t1, a0, A, B, C
            public void Init2(float transitionDuration)
            {
                // t1, a0, A, B, C
                if (v0 != 0f && (x0 / v0) < 0f)
                {
                    t1 = Mathf.Min(transitionDuration, -5f * x0 / v0);
                }
                else
                {
                    t1 = transitionDuration;
                }

                a0 = ((-8f * v0 * t1) + (-20f * x0)) / (t1 * t1);

                A = -((1f * a0 * t1 * t1) + (6f * v0 * t1) + (12f * x0)) / (2f * Mathf.Pow(t1, 5f));
                B = ((3f * a0 * t1 * t1) + (16f * v0 * t1) + (30f * x0)) / (2f * Mathf.Pow(t1, 4f));
                C = -((3f * a0 * t1 * t1) + (12f * v0 * t1) + (20f * x0)) / (2f * Mathf.Pow(t1, 3f));

                isTransition = true;
            }

            // Calculate x(t)
            public float Calculate(float t)
            {
                if (t > t1)
                {
                    t = t1;
                    isTransition = false; // Finish Transition
                }

                float t_2 = t * t;
                float t_3 = t_2 * t;
                float t_4 = t_3 * t;
                float t_5 = t_4 * t;

                float xt = A * t_5 + B * t_4 + C * t_3 + a0 * t_2 / 2f + v0 * t + x0;

                return xt;
            }
        }

        private class InertializationFloat : InertializationBase<float>
        {
            public override void Init1(float RequestedValue, float transitionDuration)
            {
                
                // x0, v0
                x0 = PrimaryCacheValue - RequestedValue;

                v0 = (PrimaryCacheValue - SecondaryCacheValue) / PrimaryToSecondaryDeltaSeconds;
                
                Init2(transitionDuration);
            }
        }

        private class InertializationVector3 : InertializationBase<Vector3>
        {
            public override void Init1(Vector3 RequestedValue, float transitionDuration)
            {
                // x0, v0
                var vec_x0 = PrimaryCacheValue - RequestedValue;
                x0 = vec_x0.magnitude;
                
                if (x0 > Mathf.Epsilon)
                {
                    direction = vec_x0.normalized;
                    var vec_xn1 = SecondaryCacheValue - RequestedValue;
                    var xn1 = Vector3.Dot(vec_xn1, direction);
                    v0 = (x0 - xn1) / PrimaryToSecondaryDeltaSeconds;
                }
                else
                {
                    direction = Vector3.zero;
                    v0 = 0;
                }

                Init2(transitionDuration);
            }
        }

        private class InertializationQuaternion : InertializationBase<Quaternion>
        {
            public override void Init1(Quaternion RequestedValue, float transitionDuration)
            {
                // x0, v0
                var invRequestedValue = Quaternion.Inverse(RequestedValue);
                var q0 = invRequestedValue * PrimaryCacheValue;
                q0.ToAngleAxis(out x0, out direction);

                x0 = Mathf.Repeat(x0 * Mathf.Deg2Rad + Mathf.PI, Mathf.PI * 2) - Mathf.PI;

                var qn1 = invRequestedValue * SecondaryCacheValue;
                var xn1 = Mathf.PI;
                if (Mathf.Abs(qn1.w) > Mathf.Epsilon)
                {
                    var q_vec = new Vector3(qn1.x, qn1.y, qn1.z);
                    xn1 = 2f * Mathf.Atan(Vector3.Dot(q_vec, direction) / qn1.w);
                    xn1 = Mathf.Repeat(xn1 + Mathf.PI, Mathf.PI * 2) - Mathf.PI;
                }

                float deltaAngle = x0 - xn1;
                deltaAngle = Mathf.Repeat(x0 - xn1 + Mathf.PI, Mathf.PI * 2) - Mathf.PI;

                v0 = deltaAngle / PrimaryToSecondaryDeltaSeconds;
                
                Init2(transitionDuration);
            }
        }

        private enum CurrentState
        {
            StateA,
            StateB,
        }

        // Field
        private Animator _animator;
        private PlayableGraph _graph;
        private List<AnimationClipPlayable> _animClipPlayables = new List<AnimationClipPlayable>();
        private AnimationClipPlayable _requestAnimClipPlayables;
        private AnimationPlayableOutput _output;

        private CurrentState _currState; //test
        private float _counter = 0f; // next transition counter

        private bool _isInertInitialize = false;
        private float _transitionTime = 0f;
        private float _prevDt = 0f;

        private HumanPoseHandler _handler;
        private HumanPose _humanPose;

        private InertializationFloat[] _musclesData;
        private InertializationVector3 _bodyPosData;
        private InertializationQuaternion _bodyRotData;
        
        // SerializeField
        [SerializeField] private float _transitionDuration = 1f;
        //[SerializeField] private List<AnimationClip> _animClips = null;
        [SerializeField] private AnimationClip _animClipA = null;
        [SerializeField] private AnimationClip _animClipB = null;

        void Awake()
        {
            // Create graph
            _graph = PlayableGraph.Create();
        }

        void Start()
        {
            // test state
            _currState = CurrentState.StateA;

            // Get component
            _animator = GetComponent<Animator>();
            _handler = new HumanPoseHandler(_animator.avatar, _animator.transform);

            // Create anim clip playables
            _animClipPlayables.Clear();
            /*if (_animClips.Count != 0)
            {
                for (int i = 0; i < _animClips.Count; i++)
                {
                    var clipPlayable = AnimationClipPlayable.Create(_graph, _animClips[i]);
                    _animClipPlayables.Add(clipPlayable);
                }

                _output = AnimationPlayableOutput.Create(_graph, "output", _animator);
                _output.SetSourcePlayable(_animClipPlayables[0]);
                //_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                _graph.Play();
            }*/
            if (_animClipA != null && _animClipB != null)
            {
                var clipPlayableA = AnimationClipPlayable.Create(_graph, _animClipA);
                _animClipPlayables.Add(clipPlayableA);
                var clipPlayableB = AnimationClipPlayable.Create(_graph, _animClipB);
                _animClipPlayables.Add(clipPlayableB);

                _output = AnimationPlayableOutput.Create(_graph, "output", _animator);
                _output.SetSourcePlayable(_animClipPlayables[0]);
                //_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                _graph.Play();
            }            

            // Prepare variables
            var muscleCount = HumanTrait.MuscleCount;
            _musclesData = new InertializationFloat[muscleCount];
            for (int i = 0; i < muscleCount; i++)
            {
                _musclesData[i] = new InertializationFloat();
            }

            _bodyPosData = new InertializationVector3();
            _bodyRotData = new InertializationQuaternion();

            _prevDt = 0f;
        }

        void OnDestroy()
        {   
            _graph.Destroy();
            //_handler.Dispose();
        }

        void Update()
        {
            // Input and Counter
            _isInertInitialize = false;
            
            if (Input.GetKey(KeyCode.A) && _counter <= 0f)
            {
                _currState = CurrentState.StateA;
                _isInertInitialize = true;
                _transitionTime = 0f;
                _counter = _transitionDuration; // Next available transition is after transitionDuration.
            }
            if (Input.GetKey(KeyCode.B) && _counter <= 0f)
            {
                _currState = CurrentState.StateB;
                _isInertInitialize = true;
                _transitionTime = 0f;
                _counter = _transitionDuration; // Next available transition is after transitionDuration.
            }

            // Next transition counter
            if (_counter > 0f)
            {
                _counter -= Time.deltaTime;
            }

            // Get Request Anim by Current State
            if (_currState == CurrentState.StateA)
            {
                _requestAnimClipPlayables = _animClipPlayables[0]; // test
            }
            else if (_currState == CurrentState.StateB)
            {
                _requestAnimClipPlayables = _animClipPlayables[1]; // test
            }

            // Set Current Animation Clip
            _output.SetSourcePlayable(_requestAnimClipPlayables);
        }

        void LateUpdate()
        {
            /* ====================================
             * Inertialization
             * ==================================== */

            float dt = Time.deltaTime;

            // Get current HumanPose
            _handler.GetHumanPose(ref _humanPose);

            // Init Inertialization
            if (_isInertInitialize)
            {
                // body
                _bodyPosData.Init1(_humanPose.bodyPosition, _transitionDuration);
                _bodyRotData.Init1(_humanPose.bodyRotation, _transitionDuration);

                // muscles
                for (int i = 0; i < _musclesData.Length; i++)
                {
                    _musclesData[i].Init1(_humanPose.muscles[i], _transitionDuration);
                }

                // Finish initialize, Start transition.
                _isInertInitialize = false;
            }

            // Update HumanPose during Transition : Calculate and Blend x(t)
            for (int i = 0; i < _musclesData.Length; i++)
            {
                if (_musclesData[i].isTransition)
                {
                    var xt = _musclesData[i].Calculate(_transitionTime);
                    _humanPose.muscles[i] = Mathf.Clamp(_humanPose.muscles[i] + xt, -1.0f, 1.0f);
                }
            }

            if (_bodyPosData.isTransition)
            {
                var xt = _bodyPosData.Calculate(_transitionTime);
                _humanPose.bodyPosition = _humanPose.bodyPosition + (_bodyPosData.direction * xt);
            }

            if (_bodyRotData.isTransition)
            {
                var xt = _bodyRotData.Calculate(_transitionTime);
                _humanPose.bodyRotation = _humanPose.bodyRotation * Quaternion.AngleAxis(xt * Mathf.Rad2Deg, _bodyRotData.direction);
            }

            // Set current HumanPose
            _handler.SetHumanPose(ref _humanPose);

            // Cache
            for (int i = 0; i < _musclesData.Length; i++)
            {
                _musclesData[i].SecondaryCacheValue = _musclesData[i].PrimaryCacheValue;
                _musclesData[i].PrimaryCacheValue =  _humanPose.muscles[i];
                _musclesData[i].PrimaryToSecondaryDeltaSeconds = _prevDt;
            }
            
            _bodyPosData.SecondaryCacheValue = _bodyPosData.PrimaryCacheValue;
            _bodyPosData.PrimaryCacheValue = _humanPose.bodyPosition;
            _bodyPosData.PrimaryToSecondaryDeltaSeconds = _prevDt;

            _bodyRotData.SecondaryCacheValue = _bodyRotData.PrimaryCacheValue;
            _bodyRotData.PrimaryCacheValue = _humanPose.bodyRotation;
            _bodyRotData.PrimaryToSecondaryDeltaSeconds = _prevDt;


            // Update time
            if (_transitionTime <= _transitionDuration)
            {
                _transitionTime += dt;
            }
            _prevDt = dt;
        }

        void OnDrawGizmos()
        {

        }

        void OnGUI()
        {
            GUILayout.Label("<color=red>Press A, B key to switch anim states.</color>");
            GUILayout.Label($"CurrentState: {_currState}");
            GUILayout.Label($"TransitionTime: {_transitionTime}");

            /*for (int i = 0; i < _musclesData.Length; i++)
            {
                if (_musclesData[i].isTransition)
                {
                    GUILayout.Label($"<color=cyan>{_musclesData[i].isTransition} : {HumanTrait.MuscleName[i]}</color>");
                }
                else
                {
                    GUILayout.Label($"<color=red>{_musclesData[i].isTransition} : {HumanTrait.MuscleName[i]}</color>");
                }
            }*/
        }
    }
}