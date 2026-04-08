# Copilot Instructions

## Project Guidelines
- When changing navigation/pages, keep the existing 'Настройки' page intact and add new functionality as a separate additional page/button instead of replacing settings.
- In this project, the terms 'включить', 'выключить', 'подключить', 'настройка', 'настроить', 'отключение', and 'включение' should be classified as the type of work 'Настройка' rather than 'Ремонт'.
- If the description does not contain explicit words related to repair, classify the task with a higher probability as 'Настройка'. Descriptions in annotations with brief code formulations without explicit repair markers should also be classified as 'Настройка'.
- Words and derivatives related to repair operations (разборка, демонтаж, снятие, очистка, дефектовка, восстановление, шлифовка, замена, монтаж, сборка, сверление, резка, покраска, подварка, и др.) should be considered by the parser as indicators of the type 'Ремонт'.