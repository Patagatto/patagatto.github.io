// File: Controllers/ExamController.cs
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QuizEsameFinale.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExamController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        // Constructor for Dependency Injection to resolve physical file paths
        public ExamController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // GET: api/exam/questions
        // Reads Esame.csv using comma separation, randomly selects 30 questions, and hides correct answers from the client
        [HttpGet("questions")]
        public IActionResult GetRandomQuestions()
        {
            try
            {
                var filePath = Path.Combine(_env.ContentRootPath, "Data", "Esame.csv");

                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound(new { error = $"Esame.csv file not found at path: {filePath}" });
                }

                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length <= 1)
                {
                    return BadRequest(new { error = "The CSV file is empty or contains only headers." });
                }

                var questionsList = new List<ExamQuestionModel>();

                // Parse CSV lines using comma (',') as the exact column delimiter
                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var columns = line.Split(',');
                    if (columns.Length >= 6)
                    {
                        questionsList.Add(new ExamQuestionModel
                        {
                            Id = questionsList.Count + 1,
                            QuestionText = columns[0].Trim().Trim('"'),
                            Options = new List<string>
                            {
                                columns[1].Trim().Trim('"'),
                                columns[2].Trim().Trim('"'),
                                columns[3].Trim().Trim('"'),
                                columns[4].Trim().Trim('"')
                            },
                            Solution = columns[5].Trim().Trim('"') // Correct answer stored securely on backend
                        });
                    }
                }

                if (questionsList.Count == 0)
                {
                    return BadRequest(new { error = "No valid questions parsed from CSV. Please check column format." });
                }

                // Randomly select up to 30 questions using LINQ and Guid shuffling
                var randomQuestions = questionsList.OrderBy(q => Guid.NewGuid()).Take(30).ToList();

                // Save active session in static memory store
                StaticExamStore.ActiveExamSession = randomQuestions;

                // Sanitize payload by removing the 'Solution' property before sending to client
                var sanitizedQuestions = randomQuestions.Select(q => new
                {
                    id = q.Id,
                    question = q.QuestionText,
                    options = q.Options.OrderBy(o => Guid.NewGuid()).ToList() // Shuffle options for fairness
                }).ToList();

                return Ok(sanitizedQuestions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Internal server error: {ex.Message}" });
            }
        }

        // POST: api/exam/submit
        // Evaluates user answers when 'Finish' is clicked, returning score and mistakes breakdown
        [HttpPost("submit")]
        public IActionResult SubmitExam([FromBody] List<UserAnswerDto> userAnswers)
        {
            if (StaticExamStore.ActiveExamSession == null || StaticExamStore.ActiveExamSession.Count == 0)
            {
                return BadRequest(new { error = "No active exam session found. Please restart the quiz." });
            }

            int correctCount = 0;
            var mistakes = new List<object>();

            foreach (var answer in userAnswers)
            {
                var question = StaticExamStore.ActiveExamSession.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question != null)
                {
                    bool isCorrect = string.Equals(question.Solution, answer.SelectedOption, StringComparison.OrdinalIgnoreCase);

                    if (isCorrect)
                    {
                        correctCount++;
                    }
                    else
                    {
                        mistakes.Add(new
                        {
                            questionId = question.Id,
                            question = question.QuestionText,
                            userAnswer = string.IsNullOrEmpty(answer.SelectedOption) ? "No answer provided" : answer.SelectedOption,
                            correctAnswer = question.Solution
                        });
                    }
                }
            }

            int totalQuestions = StaticExamStore.ActiveExamSession.Count;
            int wrongCount = totalQuestions - correctCount;

            return Ok(new
            {
                total = totalQuestions,
                correct = correctCount,
                wrong = wrongCount,
                mistakes = mistakes
            });
        }
    }

    public class ExamQuestionModel
    {
        public int Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string Solution { get; set; } = string.Empty;
    }

    public class UserAnswerDto
    {
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty;
    }

    public static class StaticExamStore
    {
        public static List<ExamQuestionModel> ActiveExamSession { get; set; } = new();
    }
}