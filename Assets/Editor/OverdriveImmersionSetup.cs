using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;

public static class OverdriveImmersionSetup
{
    const string Folder = "Assets/Audio/Immersion";
    // Original synthesized assets, generated once and imported as ordinary audio clips.
    public static AudioClip Clip(string name, float seconds, Func<float,float,float> sample)
    {
        Directory.CreateDirectory(Folder);
        string path = Folder + "/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int rate = 44100; int count = Mathf.RoundToInt(rate * seconds);
            var random = new System.Random(27); float filtered = 0;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);
                writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);
                for(int i=0;i<count;i++)
                {
                    filtered=Mathf.Lerp(filtered,(float)random.NextDouble()*2-1,.18f);
                    float value=sample((float)i/rate,filtered);
                    // Zero both ends to avoid boundary clicks on loops and one-shots.
                    float fade=Mathf.Min(1,Mathf.Min(i,count-1-i)/(rate*.008f));
                    writer.Write((short)(Mathf.Clamp(value*fade,-1,1)*26000));
                }
            }
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(AudioImporter)AssetImporter.GetAtPath(path);
        var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat=AudioCompressionFormat.PCM;importer.defaultSampleSettings=settings;
        importer.forceToMono=true;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Overdrive/Apply Racing Immersion")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var cars=UnityEngine.Object.FindObjectsByType<OverdriveVehicle>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        if(cars.Length==0)throw new InvalidOperationException("Open a racing scene.");
        var engine=Clip("Engine",1,(t,n)=>.42f*Mathf.Sin(t*2*Mathf.PI*250)+.22f*Mathf.Sin(t*2*Mathf.PI*500)+.12f*Mathf.Sin(t*2*Mathf.PI*750)+.06f*Mathf.Sin(t*2*Mathf.PI*1000)+n*.13f);
        var tyres=Clip("Tyres",2,(t,n)=>n*.85f+Mathf.Sin(t*2*Mathf.PI*1250+Mathf.Sin(t*2*Mathf.PI*13)*3)*.09f);
        var wind=Clip("Air",2,(t,n)=>n*.9f);
        var impact=Clip("Contact",.3f,(t,n)=>(n*.85f+Mathf.Sin(2*Mathf.PI*85*t)*.35f)*Mathf.Exp(-t*18));
        var countdown=Clip("GridTick",.12f,(t,n)=>Mathf.Sin(2*Mathf.PI*660*t)*.35f);
        var start=Clip("LightsOut",.32f,(t,n)=>(Mathf.Sin(2*Mathf.PI*1320*t)+Mathf.Sin(2*Mathf.PI*1980*t)*.3f)*.32f);
        foreach(var car in cars)
        {
            if(!car.GetComponent<VehicleSlipstream>())Undo.AddComponent<VehicleSlipstream>(car.gameObject);
            var audio=car.GetComponent<VehicleRaceAudio>();if(!audio)audio=Undo.AddComponent<VehicleRaceAudio>(car.gameObject);
            Undo.RecordObject(audio,"Racing immersion audio");
            audio.engineLoop=engine;audio.tyreLoop=tyres;audio.windLoop=wind;audio.impactClip=impact;audio.countdownClip=countdown;audio.startClip=start;
            EditorUtility.SetDirty(audio);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return scene.path+": "+cars.Length+" vehicles wired for audio and aerodynamic wakes";
    }
}
