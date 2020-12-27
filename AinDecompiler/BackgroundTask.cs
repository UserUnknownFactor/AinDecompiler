using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;

namespace AinDecompiler
{
    public abstract class BackgroundTask
    {
        protected MyBackgroundWorker backgroundWorker = null;
        protected class WorkerData
        {
            public string InputFileName;
            public string OutputFileName;
            public object ExtraData;
            public MyBackgroundWorker BackgroundWorker;
        }
        protected ProgressForm progressForm = null;
        protected ErrorsListForm errorsForm = null;
        protected int lastErrorCount = 0;
        //???

        protected abstract string TitleBarText
        {
            get;
        }

        protected abstract bool AbortOnCancel
        {
            get;
        }

        public bool Run(string inputFileName, string outputFileName)
        {
            return Run(inputFileName, outputFileName, null);
        }

        protected bool Run(string inputFileName, string outputFileName, object extraData)
        {
            backgroundWorker = new MyBackgroundWorker();
            var workerData = new WorkerData()
            {
                InputFileName = inputFileName,
                OutputFileName = outputFileName,
                ExtraData = extraData,
                BackgroundWorker = backgroundWorker
            };
            backgroundWorker.DoWork += new DoWorkEventHandler(backgroundWorker_DoWork);
            backgroundWorker.ProgressChanged += new ProgressChangedEventHandler(backgroundWorker_ProgressChanged);
            backgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(backgroundWorker_RunWorkerCompleted);
            backgroundWorker.WorkerReportsProgress = true;
            backgroundWorker.WorkerSupportsCancellation = true;

            if (Debugger.IsAttached)
            {
                backgroundWorker_DoWork(this, new DoWorkEventArgs(workerData));
                return true;
            }

            if (progressForm != null && !progressForm.IsDisposed)
            {
                progressForm.Dispose();
            }
            progressForm = new ProgressForm();
            progressForm.Text = this.TitleBarText;
            progressForm.CancelButtonPressed += new EventHandler(progressForm_CancelButtonPressed);

            if (errorsForm != null && !errorsForm.IsDisposed)
            {
                errorsForm.Dispose();
            }

            lastErrorCount = 0;
            backgroundWorker.RunWorkerAsync(workerData);
            DialogResult dialogResult = DialogResult.None;
            try
            {
                dialogResult = progressForm.ShowDialog();
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                dialogResult = DialogResult.None;
            }

            progressForm.Dispose();
            return dialogResult == DialogResult.OK;

        }

        void progressForm_CancelButtonPressed(object sender, EventArgs e)
        {
            if (backgroundWorker != null && !backgroundWorker.CancellationPending)
            {
                backgroundWorker.CancelAsync();
                if (this.AbortOnCancel)
                {
                    backgroundWorker.Abort();
                }
            }
        }

        protected virtual void RunWorkerCompleted(RunWorkerCompletedEventArgs e)
        {

        }

        void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            RunWorkerCompleted(e);
            if (progressForm != null && !progressForm.IsDisposed)
            {
                bool? result = e.Result as bool?;
                if (e.Cancelled == false && result == true)
                {
                    progressForm.OkayToClose = true;
                    progressForm.DialogResult = DialogResult.OK;
                    progressForm.Close();
                }
                else
                {
                    progressForm.OkayToClose = true;
                    progressForm.DialogResult = DialogResult.Cancel;
                    progressForm.Close();
                }
            }
        }

        void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (progressForm != null && !progressForm.IsDisposed)
            {
                if (e.ProgressPercentage >= 0 && e.ProgressPercentage <= 100)
                {
                    progressForm.SetProgress(e.ProgressPercentage);
                }

                var progressMessage = e.UserState as string;
                if (progressMessage != null)
                {
                    progressForm.label.Text = progressMessage;
                }
            }


            {
                var errors = e.UserState as IList<string>;
                if (errors != null && errors.Count > 0)
                {
                    if (lastErrorCount != errors.Count)
                    {
                        if (errorsForm == null || errorsForm.IsDisposed)
                        {
                            errorsForm = new ErrorsListForm();
                            errorsForm.Show();
                        }
                        errorsForm.SetErrorList(errors);
                    }
                    lastErrorCount = errors.Count;
                }
            }

            var exception = e.UserState as Exception;
            if (exception != null)
            {
                string errorMessage = exception.Message;

                if (errorsForm == null || errorsForm.IsDisposed)
                {
                    errorsForm = new ErrorsListForm();
                    errorsForm.Show();
                }
                errorsForm.AddErrorMessage(errorMessage);
            }
        }

        protected abstract object DoWork(string inputFileName, string outputFileName, WorkerData workerData);

        void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var workerData = e.Argument as WorkerData;
            string inputFileName = "";
            string outputFileName = "";
            if (workerData != null)
            {
                inputFileName = workerData.InputFileName;
                outputFileName = workerData.OutputFileName;
            }
            e.Result = DoWork(inputFileName, outputFileName, workerData);
        }
    }
}
