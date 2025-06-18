using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections;
using System.Collections.Generic;

public class TEST_REFRESH_PROBE : MonoBehaviour
{
    [SerializeField]
    private float _timeOfDayEvolution = 0f;

    [SerializeField]
    private float _dynamicProbeBaseUpdateRate = 1f;
    [SerializeField]
    private AnimationCurve _dynamicProbeUpdateCurve;

    [SerializeField]
    private ReflectionProbe _probe;
    
    [SerializeField]
    private List<ReflectionProbe> _dynamicProbeList;

    [SerializeField]
    private List<ReflectionProbe> _staticProbeList;

    [SerializeField]
    private AnimationCurve[] _weightCurves;

    private void Start()
    {
        StartCoroutine(TEST_WAIT_FOR_LOAD());
    }

    private IEnumerator TEST_WAIT_FOR_LOAD()
    {
        yield return new WaitForSeconds(1f);

        StartCoroutine(UpdateStaticProbes());
        StartCoroutine(UpdateDynamicProbes());
        yield return null;
    }

    private IEnumerator UpdateStaticProbes()
    {
        for(int i = 0; i < _staticProbeList.Count; i++)
        {
            _staticProbeList[i].RequestRenderNextUpdate();
        }

        yield return null;
    }

    private IEnumerator UpdateDynamicProbes()
    {
        yield return new WaitForSeconds(5f);
        for(int i = 0; i < _dynamicProbeList.Count; i++)
        {
            _dynamicProbeList[i].RequestRenderNextUpdate();
        }

        while(true)
        {
            for(int i = 0; i < _dynamicProbeList.Count; i++)
            {
                _dynamicProbeList[i].RequestRenderNextUpdate();
            }

            yield return null;
            yield return new WaitForSeconds(_dynamicProbeBaseUpdateRate * _dynamicProbeUpdateCurve.Evaluate(_timeOfDayEvolution));
        }
    }

    private void Update()
    {
        _timeOfDayEvolution += Time.deltaTime * 0.001f;
    }
}
