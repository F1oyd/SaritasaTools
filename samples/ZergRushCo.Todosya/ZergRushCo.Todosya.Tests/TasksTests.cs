﻿using System;
using System.Linq;
using NUnit.Framework;
using ZergRushCo.Todosya.Domain.Tasks.Commands;
using ZergRushCo.Todosya.Domain.Tasks.Handlers;

namespace ZergRushCo.Todosya.Tests
{
    /// <summary>
    /// Tasks related tests.
    /// </summary>
    [TestFixture]
    public class TasksTests
    {
        [Test]
        public void Project_related_tasks_should_be_removed_on_project_remove()
        {
            var uowFactory = new AppTestUnitOfWorkFactory();
            uowFactory.SetSeedScenario1();

            // capture before
            int totalTasksCount = 0;
            using (var uow = uowFactory.Create())
            {
                int tasksProjectCount = uow.TaskRepository.GetAll().Count(t => t.Project.Id == 1);
                totalTasksCount = uow.TaskRepository.GetAll().Count();
                Assert.That(tasksProjectCount, Is.Positive);
                Assert.That(totalTasksCount, Is.Positive);
            }

            // remove project
            new ProjectHandlers().HandleRemoveProject(new RemoveProjectCommand()
            {
                ProjectId = 1,
                UpdatedByUserId = 1
            }, uowFactory);

            // check after
            using (var uow = uowFactory.Create())
            {
                int tasksProjectCountAfter = uow.TaskRepository.GetAll().Count(t => t.Project.Id == 1);
                int totalTasksCountAfter = uow.TaskRepository.GetAll().Count();
                Assert.That(totalTasksCountAfter, Is.LessThan(totalTasksCount));
                Assert.That(tasksProjectCountAfter, Is.Zero);
            }
        }
    }
}
