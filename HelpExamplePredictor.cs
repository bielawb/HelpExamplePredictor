﻿using System.Reflection;
using System.Xml;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;

namespace PowerShell.PL.Net
{
    public class HelpExamplePredictor : ICommandPredictor
    {
        private readonly Guid _guid;
        private readonly List<string> _examples;

        internal HelpExamplePredictor(string guid)
        {
            _guid = new Guid(guid);
            _examples = new List<string>{};
            string? moduleFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(moduleFolder))
            {
                return;
            }
            string ewsHelpPath = Path.Combine(moduleFolder, "EWS-help.xml");
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(ewsHelpPath);
            XmlNamespaceManager manager = new XmlNamespaceManager(xmlDoc.NameTable);
            manager.AddNamespace("dev", "http://schemas.microsoft.com/maml/dev/2004/10");
            XmlNodeList? nodeList = xmlDoc.SelectNodes("*//dev:code", manager);
            if (nodeList is null)
            {
                return;
            }
            foreach (XmlNode node in nodeList)
            {
                foreach (string line in node.InnerText.Replace(@"PS C:\> ", "").Split(Environment.NewLine))
                {
                    _examples.Add(line);
                }
            }
        }

        /// <summary>
        /// Gets the unique identifier for a subsystem implementation.
        /// </summary>
        public Guid Id => _guid;

        /// <summary>
        /// Gets the name of a subsystem implementation.
        /// </summary>
        public string Name => "EWSPredictors";

        /// <summary>
        /// Gets the description of a subsystem implementation.
        /// </summary>
        public string Description => "Predictions based on help examples";

        /// <summary>
        /// Get the predictive suggestions. It indicates the start of a suggestion rendering session.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="context">The <see cref="PredictionContext"/> object to be used for prediction.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the prediction.</param>
        /// <returns>An instance of <see cref="SuggestionPackage"/>.</returns>
        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            if (context.TokenAtCursor.TokenFlags == TokenFlags.CommandName)
            {
                string command = context.TokenAtCursor.Text;
                
                List<PredictiveSuggestion> suggestions = new List<PredictiveSuggestion>{};
                foreach (string example in _examples)
                {
                    if (example.ToLower().StartsWith(command.ToLower()))
                    {
                        suggestions.Add(new PredictiveSuggestion(example));
                    }
                }
                if (suggestions.Count == 0)
                {
                    return default;
                }
                else
                {
                    return new SuggestionPackage(suggestions);
                }
            }
            else
            {
                return default;
            }
        }

        #region "interface methods for processing feedback"

        /// <summary>
        /// Gets a value indicating whether the predictor accepts a specific kind of feedback.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="feedback">A specific type of feedback.</param>
        /// <returns>True or false, to indicate whether the specific feedback is accepted.</returns>
        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback) => false;

        /// <summary>
        /// One or more suggestions provided by the predictor were displayed to the user.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">The mini-session where the displayed suggestions came from.</param>
        /// <param name="countOrIndex">
        /// When the value is greater than 0, it's the number of displayed suggestions from the list
        /// returned in <paramref name="session"/>, starting from the index 0. When the value is
        /// less than or equal to 0, it means a single suggestion from the list got displayed, and
        /// the index is the absolute value.
        /// </param>
        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }

        /// <summary>
        /// The suggestion provided by the predictor was accepted.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">Represents the mini-session where the accepted suggestion came from.</param>
        /// <param name="acceptedSuggestion">The accepted suggestion text.</param>
        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) { }

        /// <summary>
        /// A command line was accepted to execute.
        /// The predictor can start processing early as needed with the latest history.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="history">History command lines provided as references for prediction.</param>
        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history) { }

        /// <summary>
        /// A command line was done execution.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="commandLine">The last accepted command line.</param>
        /// <param name="success">Shows whether the execution was successful.</param>
        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) { }

        #endregion;
    }

    /// <summary>
    /// Register the predictor on module loading and unregister it on module un-loading.
    /// </summary>
    public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        private const string Identifier = "b6ac4d6f-d8d3-49ca-9891-1776f468e1fc";

        /// <summary>
        /// Gets called when assembly is loaded.
        /// </summary>
        public void OnImport()
        {
            var predictor = new HelpExamplePredictor(Identifier);
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor);
        }

        /// <summary>
        /// Gets called when the binary module is unloaded.
        /// </summary>
        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, new Guid(Identifier));
        }
    }
}