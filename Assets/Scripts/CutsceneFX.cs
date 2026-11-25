using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// เอฟเฟกต์คัตซีนที่สร้างด้วยโค้ด ไม่ต้องมีไฟล์ภาพ/เสียง:
/// - ควันดำระเบิด (Smoke Bomb) / ฝุ่นกระเด็นตอนตกกระแทกพื้น (Dust Puff)
/// - เสียงตุบกระแทกพื้น (Impact Thud) และเสียงควันฟู่ (Smoke Hiss)
/// ใช้วัสดุ Sprites/Default ที่ทำงานได้ทั้ง URP และ Built-in
/// </summary>
public static class CutsceneFX
{
    private static Material _particleMaterial;
    private static AudioClip _thudClip;
    private static AudioClip _hissClip;
    private static AudioClip _paperClip;
    private static AudioClip _tickClip;
    private static AudioClip _glitchClip;

    private static Material ParticleMaterial
    {
        get
        {
            if (_particleMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                _particleMaterial = new Material(shader);
                _particleMaterial.mainTexture = WaypointSprites.Glow.texture;
            }
            return _particleMaterial;
        }
    }

    private static ParticleSystem CreateSystem(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = ParticleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    /// <summary>ควันดำระเบิดตัวหนา ลอยขึ้นและคลี่ตัวช้า ๆ (ใช้ตอนแจ็คหายตัว)</summary>
    public static void SmokeBurst(Vector3 position, float scale = 1f)
    {
        var ps = CreateSystem("FX_SmokeBurst", position);

        var main = ps.main;
        main.loop = false;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.4f * scale, 3.2f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.05f, 0.05f, 0.06f, 0.9f), new Color(0.22f, 0.20f, 0.20f, 0.75f));
        main.gravityModifier = -0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)52) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f * scale;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.55f, 1f, 1.7f));

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.45f;
        noise.scrollSpeed = 0.2f;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

        ps.Play();
        PlaySmokeHiss(position);
    }

    /// <summary>ฝุ่นกระจายเป็นวงบนพื้นตอนร่างตกกระแทก</summary>
    public static void DustPuff(Vector3 position, float scale = 1f)
    {
        var ps = CreateSystem("FX_DustPuff", position + Vector3.up * 0.05f);

        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.1f * scale);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.47f, 0.38f, 0.5f), new Color(0.40f, 0.34f, 0.28f, 0.4f));
        main.gravityModifier = -0.01f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f * scale;
        shape.rotation = new Vector3(90f, 0f, 0f); // ให้วงกลมนอนราบบนพื้น

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));

        ps.Play();
    }

    public static void PlayThud(Vector3 position, float volume = 1f)
    {
        if (_thudClip == null) _thudClip = BuildThudClip();
        AudioSource.PlayClipAtPoint(_thudClip, position, volume);
    }

    public static void PlaySmokeHiss(Vector3 position, float volume = 0.6f)
    {
        if (_hissClip == null) _hissClip = BuildHissClip();
        AudioSource.PlayClipAtPoint(_hissClip, position, volume);
    }

    /// <summary>เสียงพิมพ์ดีด/แป้นคีย์บอร์ดสั้น ๆ (ใช้กับข้อความเทอร์มินัล)</summary>
    public static void PlayTick(Vector3 position, float volume = 0.25f)
    {
        if (_tickClip == null)
        {
            const int sr = 44100;
            int n = (int)(sr * 0.035f);
            var d = new float[n];
            var rng = new System.Random(5);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                d[i] = (((float)rng.NextDouble() * 2f - 1f) * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 1800f) * 0.4f) * Mathf.Exp(-t * 140f);
            }
            _tickClip = AudioClip.Create("Tick", n, 1, sr, false);
            _tickClip.SetData(d, 0);
        }
        AudioSource.PlayClipAtPoint(_tickClip, position, volume);
    }

    /// <summary>เสียงซ่าและบิดเบี้ยวของระบบที่ถูกแฮ็ก</summary>
    public static void PlayGlitch(Vector3 position, float volume = 0.6f)
    {
        if (_glitchClip == null)
        {
            const int sr = 44100;
            int n = (int)(sr * 0.7f);
            var d = new float[n];
            var rng = new System.Random(31);
            float hold = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)sr;
                if (i % 220 == 0) hold = (float)rng.NextDouble() * 2f - 1f;
                float saw = Mathf.Repeat(t * (90f + 700f * Mathf.PingPong(t * 6f, 1f)), 1f) * 2f - 1f;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 2.2f);
                d[i] = Mathf.Clamp((hold * 0.5f + saw * 0.5f) * env, -1f, 1f);
            }
            _glitchClip = AudioClip.Create("Glitch", n, 1, sr, false);
            _glitchClip.SetData(d, 0);
        }
        AudioSource.PlayClipAtPoint(_glitchClip, position, volume);
    }

    /// <summary>เสียงกระดาษ/แฟ้มพลิก: สวูชนุ่ม ๆ ใช้กับ UI แฟ้มคดี</summary>
    public static void PlayPaperFlip(Vector3 position, float volume = 0.6f)
    {
        if (_paperClip == null) _paperClip = BuildPaperClip();
        AudioSource.PlayClipAtPoint(_paperClip, position, volume);
    }

    private static AudioClip BuildPaperClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.38f;
        int count = (int)(sampleRate * duration);
        var samples = new float[count];
        var rng = new System.Random(23);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float white = (float)rng.NextDouble() * 2f - 1f;
            low += 0.55f * (white - low);
            float band = white - low; // เน้นความถี่สูงกลาง คล้ายเสียงกระดาษเสียดสี
            float env = Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t * 8f);
            samples[i] = band * env * 0.35f;
        }
        var clip = AudioClip.Create("PaperFlip", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip BuildThudClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.6f;
        int count = (int)(sampleRate * duration);
        var samples = new float[count];
        var rng = new System.Random(7);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float freq = Mathf.Lerp(95f, 34f, Mathf.Clamp01(t / 0.25f));
            phase += 2f * Mathf.PI * freq / sampleRate;
            float body = Mathf.Sin(phase) * Mathf.Exp(-t * 9f);
            float click = ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 55f) * 0.55f;
            samples[i] = Mathf.Clamp(body * 0.95f + click, -1f, 1f);
        }
        var clip = AudioClip.Create("ImpactThud", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip BuildHissClip()
    {
        const int sampleRate = 44100;
        const float duration = 1.5f;
        int count = (int)(sampleRate * duration);
        var samples = new float[count];
        var rng = new System.Random(11);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)sampleRate;
            float white = (float)rng.NextDouble() * 2f - 1f;
            low += 0.35f * (white - low);
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 2.6f);
            samples[i] = (white * 0.35f + low * 0.65f) * env * 0.5f;
        }
        var clip = AudioClip.Create("SmokeHiss", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// ฟีลกล้องภาพยนตร์ผ่าน Cinemachine Noise: กล้องสั่นเบา ๆ แบบถือด้วยมือ (Handheld) ตลอดช็อต
/// และแรงสั่นกระแทก (Impact Shake) ที่ค่อยจางลง ไม่ไปแย่งตำแหน่งกล้องกับระบบเคลื่อนกล้องอื่น
/// </summary>
public static class CinematicCameraFeel
{
    private static NoiseSettings _profile;

    private static NoiseSettings Profile
    {
        get
        {
            if (_profile == null)
            {
                _profile = ScriptableObject.CreateInstance<NoiseSettings>();
                _profile.name = "RuntimeHandheld";
                _profile.PositionNoise = new[]
                {
                    new NoiseSettings.TransformNoiseParams { X = Noise(0.04f, 0.55f), Y = Noise(0.05f, 0.7f), Z = Noise(0.03f, 0.4f) }
                };
                _profile.OrientationNoise = new[]
                {
                    new NoiseSettings.TransformNoiseParams { X = Noise(0.35f, 0.45f), Y = Noise(0.45f, 0.35f), Z = Noise(0.20f, 0.30f) }
                };
            }
            return _profile;
        }
    }

    private static NoiseSettings.NoiseParams Noise(float amplitude, float frequency)
    {
        return new NoiseSettings.NoiseParams { Amplitude = amplitude, Frequency = frequency };
    }

    public static CinemachineBasicMultiChannelPerlin Ensure(CinemachineCamera cam)
    {
        if (cam == null) return null;
        var noise = cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise == null) noise = cam.gameObject.AddComponent<CinemachineBasicMultiChannelPerlin>();
        noise.NoiseProfile = Profile;
        return noise;
    }

    /// <summary>ตั้งความสั่นพื้นฐานของกล้อง (0 = นิ่งสนิท, 1 = ถือกล้องด้วยมือเบา ๆ, 2+ = วิ่งถือกล้อง)</summary>
    public static void SetHandheld(CinemachineCamera cam, float amplitudeGain, float frequencyGain = 1f)
    {
        var noise = Ensure(cam);
        if (noise == null) return;
        noise.AmplitudeGain = amplitudeGain;
        noise.FrequencyGain = frequencyGain;
    }

    public static void Clear(CinemachineCamera cam)
    {
        if (cam == null) return;
        var noise = cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
        if (noise != null) noise.AmplitudeGain = 0f;
    }

    /// <summary>แรงสั่นกระแทก: เพิ่มแอมพลิจูดและความถี่สูงสุดทันที แล้วจางลงเป็นเส้นโค้ง</summary>
    public static IEnumerator ShakeRoutine(CinemachineCamera cam, float peakGain, float duration)
    {
        var noise = Ensure(cam);
        if (noise == null) yield break;

        float baseGain = noise.AmplitudeGain;
        float baseFreq = noise.FrequencyGain;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            noise.AmplitudeGain = baseGain + peakGain * k * k;
            noise.FrequencyGain = baseFreq + 2.5f * k;
            yield return null;
        }
        noise.AmplitudeGain = baseGain;
        noise.FrequencyGain = baseFreq;
    }
}
