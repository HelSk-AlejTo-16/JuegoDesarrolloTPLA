#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Generador y configurador de animaciones y AnimatorController para Enemy-Punk (Jefe Nivel 2).
/// Crea clips de animación para Idle, Walk, Punch y Hurt, y los conecta en un AnimatorController.
/// </summary>
public static class EnemyPunkAnimationBuilder
{
    private const string PunkFolder = "Assets/NewStuff/Streets of Fight files/Assets/Sprites/Enemy-Punk";
    private const string AnimOutputFolder = "Assets/Animations/Enemy_Punk";
    private const string ControllerPath = "Assets/Animations/Enemy_Punk/EnemyPunk_Controller.controller";
    private const string BossDataPath = "Assets/Data/Enemies/Jefe_CapatazBlindado_N2.asset";

    [MenuItem("RPG Santa Felipina/Generar Animaciones y Controlador Enemy-Punk")]
    public static AnimatorController BuildEnemyPunkAnimationsAndController()
    {
        if (!Directory.Exists(AnimOutputFolder))
        {
            Directory.CreateDirectory(AnimOutputFolder);
        }

        // 1. Cargar sprites individuales de cada subcarpeta
        Sprite[] idleSprites = LoadSprites($"{PunkFolder}/Idle", "idle", 4);
        Sprite[] walkSprites = LoadSprites($"{PunkFolder}/Walk", "walk", 4);
        Sprite[] punchSprites = LoadSprites($"{PunkFolder}/Punch", "punch", 3);
        Sprite[] hurtSprites = LoadSprites($"{PunkFolder}/Hurt", "hurt", 4);

        if (idleSprites == null || idleSprites.Length == 0)
        {
            Debug.LogError($"[EnemyPunkAnimationBuilder] No se pudieron cargar los sprites de Idle desde {PunkFolder}/Idle");
            return null;
        }

        // 2. Crear AnimationClips
        AnimationClip idleClip = CreateSpriteClip($"{AnimOutputFolder}/EnemyPunk_Idle.anim", idleSprites, 6f, loop: true);
        AnimationClip walkClip = CreateSpriteClip($"{AnimOutputFolder}/EnemyPunk_Walk.anim", walkSprites, 8f, loop: true);
        AnimationClip punchClip = CreateSpriteClip($"{AnimOutputFolder}/EnemyPunk_Punch.anim", punchSprites, 10f, loop: false);
        AnimationClip hurtClip = CreateSpriteClip($"{AnimOutputFolder}/EnemyPunk_Hurt.anim", hurtSprites, 8f, loop: false);

        // 3. Crear / Reemplazar AnimatorController
        if (File.Exists(ControllerPath))
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRunning", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        // Estado Idle
        AnimatorState idleState = sm.AddState("Idle", new Vector3(250, 60, 0));
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        // Estado Walk
        AnimatorState walkState = sm.AddState("Walk", new Vector3(250, 180, 0));
        walkState.motion = walkClip;

        AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0f;
        toWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking");

        AnimatorStateTransition fromWalk = walkState.AddTransition(idleState);
        fromWalk.hasExitTime = false;
        fromWalk.duration = 0f;
        fromWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");

        // Estado Punch (Ataque)
        AnimatorState punchState = sm.AddState("Punch", new Vector3(520, 60, 0));
        punchState.motion = punchClip;

        AnimatorStateTransition toPunch = sm.AddAnyStateTransition(punchState);
        toPunch.hasExitTime = false;
        toPunch.duration = 0f;
        toPunch.AddCondition(AnimatorConditionMode.If, 0, "Attack");

        AnimatorStateTransition fromPunch = punchState.AddTransition(idleState);
        fromPunch.hasExitTime = true;
        fromPunch.exitTime = 1.0f;
        fromPunch.duration = 0f;

        // Estado Hurt (Daño)
        AnimatorState hurtState = sm.AddState("Hurt", new Vector3(520, 180, 0));
        hurtState.motion = hurtClip;

        AnimatorStateTransition toHurt = sm.AddAnyStateTransition(hurtState);
        toHurt.hasExitTime = false;
        toHurt.duration = 0f;
        toHurt.AddCondition(AnimatorConditionMode.If, 0, "Hurt");

        AnimatorStateTransition fromHurt = hurtState.AddTransition(idleState);
        fromHurt.hasExitTime = true;
        fromHurt.exitTime = 1.0f;
        fromHurt.duration = 0f;

        AssetDatabase.SaveAssets();

        // 4. Asignar los frames y el controller al EnemyData del Jefe
        EnemyData bossData = AssetDatabase.LoadAssetAtPath<EnemyData>(BossDataPath);
        if (bossData != null)
        {
            bossData.enemyName = "El Capataz Punk";
            bossData.enemyType = EnemyType.JefeNivel2Capataz;
            bossData.maxHP = 185;
            bossData.attack = 22;
            bossData.defense = 8;
            bossData.motricidad = 12;
            bossData.EXP_Reward = 260;
            bossData.primaryAttackName = "Gancho Callejero Punk";
            bossData.secondaryAttackName = "Ráfaga de Nudillos";
            bossData.placeholderColor = new Color(0.95f, 0.35f, 0.20f, 1f);

            bossData.animatorController = controller;
            bossData.enemySprite = idleSprites[0];
            bossData.idleFrames = idleSprites;
            bossData.idleAltFrames = idleSprites;
            bossData.walkFrames = walkSprites;
            bossData.runFrames = walkSprites;
            bossData.attackFrames = punchSprites;
            bossData.attack2Frames = punchSprites;
            bossData.hurtFrames = hurtSprites;
            bossData.deadFrames = new Sprite[] { hurtSprites[2], hurtSprites[3] };

            EditorUtility.SetDirty(bossData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[EnemyPunkAnimationBuilder] ¡EnemyData '{BossDataPath}' actualizado exitosamente con sprites y animaciones de Enemy-Punk!");
        }

        Debug.Log($"[EnemyPunkAnimationBuilder] ¡Clips y AnimatorController creados exitosamente en '{ControllerPath}'!");
        return controller;
    }

    public static Sprite[] LoadSprites(string folder, string baseName, int count)
    {
        List<Sprite> list = new List<Sprite>();
        for (int i = 1; i <= count; i++)
        {
            string path = $"{folder}/{baseName}{i}.png";
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null)
            {
                Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
                if (all != null)
                {
                    foreach (var o in all)
                    {
                        if (o is Sprite s) { sp = s; break; }
                    }
                }
            }
            if (sp != null)
            {
                list.Add(sp);
            }
            else
            {
                Debug.LogWarning($"[EnemyPunkAnimationBuilder] No se encontró sprite en: {path}");
            }
        }
        return list.ToArray();
    }

    private static AnimationClip CreateSpriteClip(string clipPath, Sprite[] sprites, float fps, bool loop)
    {
        if (sprites == null || sprites.Length == 0) return null;

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        bool isNew = (clip == null);
        if (isNew) clip = new AnimationClip();
        else clip.ClearCurves();

        clip.frameRate = fps;

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[sprites.Length];
        float frameTime = 1f / fps;
        for (int i = 0; i < sprites.Length; i++)
        {
            keyFrames[i] = new ObjectReferenceKeyframe
            {
                time = i * frameTime,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyFrames);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, clipPath);
        else EditorUtility.SetDirty(clip);

        return clip;
    }
}
#endif
