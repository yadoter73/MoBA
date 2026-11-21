using System;
using System.Collections.Generic;
using DialogueEditor;
using UnityEngine;

namespace MoBA.Narrative
{
    public enum DialogueBranchKind
    {
        Intro = 0,
        Repeat = 1,
        Final = 2
    }

    /// <summary>
    /// Единая логика выбора диалога NPC, гарантирующая, что одноразовые диалоги (вступление/финал)
    /// не проигрываются повторно, а при повторном обращении выдаётся короткая реплика-напоминание.
    /// </summary>
    public sealed class OneTimeConversationGate
    {
        private NPCConversation _runtimeRepeatConversation;

        public bool IsIntroConsumed { get; private set; }
        public bool IsFinalConsumed { get; private set; }

        public DialogueBranchKind SelectConversation(
            Transform owner,
            bool wantIntro,
            bool wantFinal,
            NPCConversation introConversation,
            NPCConversation finalConversation,
            NPCConversation serializedRepeatConversation,
            string defaultSpeakerName,
            Func<string> getRuntimeRepeatText,
            out NPCConversation selectedConversation)
        {
            if (wantIntro && !IsIntroConsumed && introConversation != null)
            {
                IsIntroConsumed = true;
                selectedConversation = introConversation;
                return DialogueBranchKind.Intro;
            }

            if (wantFinal && !IsFinalConsumed && finalConversation != null)
            {
                IsFinalConsumed = true;
                selectedConversation = finalConversation;
                return DialogueBranchKind.Final;
            }

            if (serializedRepeatConversation != null)
            {
                selectedConversation = serializedRepeatConversation;
                return DialogueBranchKind.Repeat;
            }

            selectedConversation = GetOrUpdateRuntimeRepeatConversation(
                owner,
                introConversation,
                defaultSpeakerName,
                getRuntimeRepeatText != null ? getRuntimeRepeatText() : null);

            return DialogueBranchKind.Repeat;
        }

        private NPCConversation GetOrUpdateRuntimeRepeatConversation(
            Transform owner,
            NPCConversation templateConversation,
            string defaultSpeakerName,
            string lineText)
        {
            if (_runtimeRepeatConversation == null)
            {
                GameObject go = new GameObject("RuntimeRepeatConversation");
                if (owner != null)
                {
                    go.transform.SetParent(owner, false);
                }

                _runtimeRepeatConversation = go.AddComponent<NPCConversation>();
                if (templateConversation != null)
                {
                    _runtimeRepeatConversation.DefaultName = templateConversation.DefaultName;
                    _runtimeRepeatConversation.DefaultFont = templateConversation.DefaultFont;
                    _runtimeRepeatConversation.ContinueFont = templateConversation.ContinueFont;
                    _runtimeRepeatConversation.EndConversationFont = templateConversation.EndConversationFont;
                }
                else
                {
                    _runtimeRepeatConversation.DefaultName = string.IsNullOrEmpty(defaultSpeakerName) ? "NPC" : defaultSpeakerName;
                }
            }

            string speaker = string.IsNullOrEmpty(_runtimeRepeatConversation.DefaultName)
                ? (string.IsNullOrEmpty(defaultSpeakerName) ? "NPC" : defaultSpeakerName)
                : _runtimeRepeatConversation.DefaultName;
            string safeText = string.IsNullOrEmpty(lineText) ? "..." : lineText;

            var ec = new EditableConversation
            {
                Parameters = new List<EditableParameter>()
            };

            var node = new EditableSpeechNode
            {
                ID = 151,
                Name = speaker,
                Text = safeText,
                TMPFont = _runtimeRepeatConversation.DefaultFont,
                AdvanceDialogueAutomatically = true,
                AutoAdvanceShouldDisplayOption = true,
                TimeUntilAdvance = 5.0f,
                Volume = 1f,
                ParamActions = new List<EditableSetParamAction>(),
                Connections = new List<EditableConnection>(),
                EditorInfo = new EditableConversationNode.EditorArgs
                {
                    isRoot = true,
                    xPos = 160f,
                    yPos = 80f
                }
            };

            ec.SpeechNodes.Add(node);
            node.RegisterUIDs();
            node.SerializeAssetData(_runtimeRepeatConversation);
            _runtimeRepeatConversation.CurrentIDCounter = 152;
            _runtimeRepeatConversation.Serialize(ec);
            return _runtimeRepeatConversation;
        }
    }
}
