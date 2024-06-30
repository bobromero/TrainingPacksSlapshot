using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TrainingPacks.Scenario;

namespace TrainingPacks {
    public class Trainer : MelonMod {
        public Pack CurrentPack = new Pack();
        public Pack NewPack = new Pack();
        public GameObject? NameSetter;
        public TMP_InputField? inputField;
        private UiCode ui = new UiCode();
        public UiCode TrainingUi = new UiCode();
        public float NumPacks = 0f;
        public string directory = Path.Combine(Application.dataPath, "TrainingPacks");

        public string GetCurrentPackName() {
            return CurrentPack.Name;
        }
        public override void OnGUI() {
            if (ui._isGuiVisible) {
                ui._guiWindowRect = GUI.Window(0, ui._guiWindowRect, (GUI.WindowFunction)ui.MainGuiWindowFunction, "Training Packs Mod");
            }
            if (TrainingUi._isGuiVisible) {
                TrainingUi._guiWindowRect = GUI.Window(0, TrainingUi._guiWindowRect, (GUI.WindowFunction)TrainingUi.PackSelectGuiWindowFunction, "Pick Training Pack");
            }
        }

        public override void OnUpdate() {
            if (!AppManager.Instance.IsInOnlineGame() && Input.GetKeyDown(KeyCode.F1)) {
                Debug.Log("Menu Toggled");
                ui._isGuiVisible = !ui._isGuiVisible;
                TrainingUi._isGuiVisible = false;
                Cursor.lockState = ui._isGuiVisible ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = !Cursor.visible;
            }
            // Update the color lerp time
            ui._colorLerpTime += ui._colorChangeSpeed * Time.deltaTime;
            if (ui._colorLerpTime > 1f)
                ui._colorLerpTime -= 1f;

            if (AppManager.Instance.game != null && AppManager.Instance.game.IsOnlineMatch) {
                return;
            }

            if (CurrentPack != null && Input.GetKeyDown(KeyCode.RightArrow)) {
                LoadScene(true);
            }
            if (CurrentPack != null && Input.GetKeyDown(KeyCode.LeftArrow)) {
                LoadScene(false);
            }
        }

        public void SetPackScene() {
            NewPack.AddScene();
        }

        public void SavePack(string name) {
            Melon<Trainer>.Logger.Msg("Saving pack");
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }
            BinarySerialization.WriteToBinaryFile<Pack>(Path.Combine(directory, name + ".tpack"), NewPack);
            NewPack = new Pack();
        }

        public void LoadPack() {
            TrainingUi._isGuiVisible = true;
            TrainingUi.FileNames = Directory.CreateDirectory(directory).GetFiles().Select(name => name.Name).ToList();
        }

        public void LoadScene(bool nextScene) {
            Scenario scene = CurrentPack.GetScenes()[CurrentPack.CurrentScene + (nextScene ? (CurrentPack.CurrentScene + 1 >= CurrentPack.NumScenes ? -CurrentPack.CurrentScene : 1) : (CurrentPack.CurrentScene - 1 < 0 ? CurrentPack.NumScenes-1 : -1))];
            CurrentPack.CurrentScene = scene.id;
            Game game = AppManager.Instance.game;
            game.ResetPositions();
            game.customPlayerSpawn = false;
            Chatbox.Instance.AddMessage("Training Packs", Color.magenta, "Scene: " +  (scene.id + 1));

            if (scene.side != game.localPlayer.team && !scene.IsFlipped && !game.localPlayer.IsCameraFlipped()) {
                game.localPlayer.FlipCamera();
            }
            foreach (var obstacle in scene.Obstacles) {
                switch (obstacle.type) {
                    case Obstacle.Player:
                        if (obstacle.NeedToSpawn) {
                            Melon<Trainer>.Logger.Msg("Error Can't spawn player twice");
                        }
                        if (!game.customPlayerSpawn) {
                            game.localPlayer.playerController.SetPosition(ObstaclePosToVector3(obstacle));
                            game.localPlayer.playerController.SetStickRotation(obstacle.rotation);
                            game.localPlayer.RequestSetPlayerSpawn();                                
                        }
                        break;
                    case Obstacle.Puck:
                        if (obstacle.NeedToSpawn) {
                            byte puckNum = AppManager.Instance.game.SpawnPuck().Id;
                            game.Pucks[puckNum].transform.position = ObstaclePosToVector3(obstacle);
                        }
                        break;
                    case Obstacle.PuckSpawn:
                        game.puckSpawnPoint = ObstaclePosToVector3(obstacle);
                        game.customPuckSpawn = true;
                        break;
                    case Obstacle.PuckTarget:
                        game.customPuckTarget = true;
                        game.localPlayer.yoyoPuck.currentPuckTargetPos = ObstaclePosToVector3(obstacle);
                        break;
                    default:
                        Melon<Trainer>.Logger.Msg("Error type not found");
                        break;
                }
            }
            game.ResetPractice();
        }

        private Vector3 ObstaclePosToVector3(ObstacleInfo oi) {
            return new Vector3(oi.position[0], oi.position[1], oi.position[2]);
        }

    }
    [Serializable]
    public class Pack
    {
        public string Name = string.Empty;
        public byte CurrentScene = 0;
        public byte NumScenes = 0;
        private Scenario[] Scenes = new Scenario[] {};
        
        public Scenario[] GetScenes() {
            return Scenes;
        }

        public void AddScene() {
            NumScenes++;
            CurrentScene = (byte)(NumScenes - 1);
            Scenario[] oldScenes = new Scenario[Scenes.Length];
            for (int i = 0; i < Scenes.Length; i++) {
                oldScenes[i] = Scenes[i];
            }
            Scenes = new Scenario[NumScenes];
            for (int i = 0; i < NumScenes - 1; i++) {
                Scenes[i] = oldScenes[i];
            }
            Scenes[CurrentScene] = GetStateAsScene(CurrentScene);
        }

        private Scenario GetStateAsScene(byte SceneNum) {
            int NumObstacles = 1;
            ObstacleInfo[] newInfo = new ObstacleInfo[NumObstacles];
            Game game = AppManager.Instance.game;
            newInfo[NumObstacles - 1].NeedToSpawn = false;
            newInfo[NumObstacles - 1].type = Obstacle.Player;
            newInfo[NumObstacles - 1].position = new float[3] { game.localPlayer.yoyoPuck.currentPlayerSpawnPos.x, game.localPlayer.yoyoPuck.currentPlayerSpawnPos.y, game.localPlayer.yoyoPuck.currentPlayerSpawnPos.z };
            newInfo[NumObstacles - 1].rotation = game.localPlayer.playerController.bodyMeshRotation;

            ObstacleInfo[] prevInfo = new ObstacleInfo[NumObstacles];
            for (int i = 0; i < NumObstacles; i++) {
                prevInfo[i] = newInfo[i];
            }
            NumObstacles++;
            newInfo = new ObstacleInfo[NumObstacles];
            for (int i = 0; i < NumObstacles - 1; i++) {
                newInfo[i] = prevInfo[i];
            }
            newInfo[NumObstacles - 1].NeedToSpawn = true;
            newInfo[NumObstacles - 1].type = Obstacle.PuckSpawn;
            newInfo[NumObstacles - 1].position = new float[3] { game.puckSpawnPoint.x, game.puckSpawnPoint.y, game.puckSpawnPoint.z };

            foreach (Puck puck in game.Pucks.values) {
                ObstacleInfo[] oldInfo = new ObstacleInfo[NumObstacles];
                for (int i = 0; i < NumObstacles; i++) {
                    oldInfo[i] = newInfo[i];
                }
                NumObstacles++;
                newInfo = new ObstacleInfo[NumObstacles];
                for (int i = 0; i < NumObstacles - 1; i++) {
                    newInfo[i] = oldInfo[i];
                }
                newInfo[NumObstacles - 1].NeedToSpawn = puck.Id > 0;
                newInfo[NumObstacles - 1].type = Obstacle.Puck;
                newInfo[NumObstacles - 1].position = new float[3] { puck.transform.position.x, puck.transform.position.y , puck.transform.position.z };
            }
            if (game.customPuckTarget) {
                ObstacleInfo targetInfo = new ObstacleInfo();
                targetInfo.NeedToSpawn = true;
                targetInfo.type = Obstacle.PuckTarget;
                targetInfo.position = new float[3] { game.localPlayer.yoyoPuck.currentPuckTargetPos.x, game.localPlayer.yoyoPuck.currentPuckTargetPos.y, game.localPlayer.yoyoPuck.currentPuckTargetPos.z };

                ObstacleInfo[] oldInfo = new ObstacleInfo[NumObstacles];
                for (int i = 0; i < NumObstacles; i++) {
                    oldInfo[i] = newInfo[i];
                }
                NumObstacles++;
                newInfo = new ObstacleInfo[NumObstacles];
                for (int i = 0; i < NumObstacles - 1; i++) {
                    newInfo[i] = oldInfo[i];
                }
                newInfo[NumObstacles - 1] = targetInfo;
            }
            Scenario CurrentScene = new Scenario(SceneNum, newInfo, game.localPlayer.team, game.localPlayer.IsCameraFlipped());
            return CurrentScene;
        }
    }

    [Serializable]
    public class Scenario {
        [Serializable]
        public enum Obstacle
        {
            Player,
            Puck,
            PuckSpawn,
            PuckTarget,
        }
        [Serializable]
        public struct ObstacleInfo {
            public bool NeedToSpawn;
            public Obstacle type;
            public float[] position;
            public float rotation;
        }

        public byte id;
        public ObstacleInfo[] Obstacles;
        public Team side;
        public bool IsFlipped = false;

        public Scenario(byte SceneId, ObstacleInfo[] info, Team team, bool isCamFlipped) {
            id = SceneId;
            Obstacles = info;
            side=team;
            IsFlipped = isCamFlipped;
        }
    }

    //Baumz's ui code
    public class UiCode {
        public bool _isGuiVisible;
        public Rect _guiWindowRect = new Rect(20, 20, 400, 600);
        private Gradient? _guiColorGradient;
        public float _colorChangeSpeed = 0.35f; //Change this value to increase/decrease color change speed.
        public float _colorLerpTime;
        public int NumFiles = 0;
        public List<string> FileNames = new List<string>();

        public UiCode() {
            _guiColorGradient = CreateGradientColor();
        }

        public void MainGuiWindowFunction(int windowId) {
            // GUI content goes here
            GUI.backgroundColor = _guiColorGradient?.Evaluate(_colorLerpTime) ?? Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(100, 20, 180, 20), "Training Pack Mod");
            // Buttons Below--------------------------------------
            if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 100, 150, 30), "Save as Scene in Pack")) //Change "button 1" to change name on button.
            {
                // Button 1 action
                Melon<Trainer>.Instance.SetPackScene(); //Change "Button 1 pressed!" to what you want logged in the console.
            }
            if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 180, 150, 30), "Save as Pack")) //Change "button 2" to change name on button.
            {
                if (Melon<Trainer>.Instance.NameSetter == null) {
                    GameObject reportServer = GameObject.Find("HockeyGamemode").transform.GetChild(11).Find("Report Server").gameObject;
                    Melon<Trainer>.Instance.NameSetter = GameObject.Instantiate(reportServer);
                    Melon<Trainer>.Instance.inputField = Melon<Trainer>.Instance.NameSetter.transform.GetChild(0).GetChild(1).GetChild(1).GetChild(0).GetComponent<TMP_InputField>();

                    //hide buttons and change text
                    Melon<Trainer>.Instance.NameSetter.transform.GetChild(0).GetChild(1).GetChild(3).GetChild(1).gameObject.SetActive(false);
                    Melon<Trainer>.Instance.NameSetter.transform.GetChild(0).GetChild(0).GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>().text = "Type name of your new pack (Click save as pack again to save)";
                    Melon<Trainer>.Instance.NameSetter.SetActive(true);
                } else if (Melon<Trainer>.Instance.NameSetter.active) {
                    Melon<Trainer>.Instance.SavePack(Melon<Trainer>.Instance.inputField.text);
                    Melon<Trainer>.Instance.NameSetter.SetActive(false);
                } else
                    Melon<Trainer>.Instance.NameSetter.SetActive(true);
                InputManager.AcceptPlayerInput = !InputManager.AcceptPlayerInput;
            }
            

            if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 260, 150, 30), "Load Next Scene")) //Change "button 3" to change name on button.
            {
                // Button 3 action
                Melon<Trainer>.Instance.LoadScene(true); //Change "Button 3 pressed!" to what you want logged in the console.
            }
            if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 340, 150, 30), "Load Previous Scene")) //Change "button 4" to change name on button.
            {
                // Button 4 action
                Melon<Trainer>.Instance.LoadScene(false); //Change "Button 4 pressed!" to what you want logged in the console.
            }
            if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 420, 150, 30), "Load Pack")) //Change "button 4" to change name on button.
            {
                // Button 4 action
                Melon<Trainer>.Instance.LoadPack(); //Change "Button 4 pressed!" to what you want logged in the console.
            }
            GUI.DragWindow();
        }

        public void PackSelectGuiWindowFunction(int windowId) {
            // GUI content goes here
            GUI.backgroundColor = _guiColorGradient?.Evaluate(_colorLerpTime) ?? Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(100, 20, 180, 20), "Training Pack Mod");
            int i = 0;
            foreach (var file in FileNames) {

                if (GUI.Button(new Rect((_guiWindowRect.width) / 2, 100 + i * 40, 150, 30), file)) //Change "button 1" to change name on button.
                {
                    Melon<Trainer>.Logger.Msg("Loading Pack");
                    Melon<Trainer>.Instance.CurrentPack = BinarySerialization.ReadFromBinaryFile<Pack>(Path.Combine(Melon<Trainer>.Instance.directory, file));
                    Chatbox.Instance.AddMessage("Training Packs", Color.magenta, file + " Loaded!");
                    Melon<Trainer>.Instance.CurrentPack.CurrentScene = 0;
                    Melon<Trainer>.Logger.Msg("Number of Scenes: " + Melon<Trainer>.Instance.CurrentPack.NumScenes);
                    Melon<Trainer>.Instance.LoadScene(true);
                    Melon<Trainer>.Instance.TrainingUi._isGuiVisible = false;
                }
                i++;
            }

            GUI.VerticalScrollbar(new Rect(_guiWindowRect.right - 20, 0, 20, 600), 0, 100f, 0, Melon<Trainer>.Instance.NumPacks);
        }

        private Gradient CreateGradientColor() {
            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[7];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

            // Rainbow spectrum colors
            colorKeys[0].color = Color.red;
            colorKeys[0].time = 0f;
            colorKeys[1].color = Color.yellow;
            colorKeys[1].time = 0.17f;
            colorKeys[2].color = Color.green;
            colorKeys[2].time = 0.33f;
            colorKeys[3].color = Color.cyan;
            colorKeys[3].time = 0.5f;
            colorKeys[4].color = Color.blue;
            colorKeys[4].time = 0.67f;
            colorKeys[5].color = Color.magenta;
            colorKeys[5].time = 0.83f;
            colorKeys[6].color = Color.red;
            colorKeys[6].time = 1f;

            alphaKeys[0].alpha = 1f;
            alphaKeys[0].time = 0f;
            alphaKeys[1].alpha = 1f;
            alphaKeys[1].time = 1f;

            gradient.SetKeys(colorKeys, alphaKeys);
            gradient.mode = GradientMode.Blend; // Set the gradient mode to Blend for smooth transition

            return gradient;
        }
    }
    /// Not My code got it from https://blog.danskingdom.com/saving-and-loading-a-c-objects-data-to-an-xml-json-or-binary-file/
    public static class BinarySerialization {
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite, bool append = false) {
            using (Stream stream = File.Open(filePath, append ? FileMode.Append : FileMode.Create)) {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        public static T ReadFromBinaryFile<T>(string filePath) {
            using (Stream stream = File.Open(filePath, FileMode.Open)) {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }
    }
}