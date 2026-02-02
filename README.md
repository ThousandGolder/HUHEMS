# Holistic Examination Management System (HEMS)

## 1. Introduction
The **Holistic Examination Management System (HEMS)** is a professional-grade, web-based platform engineered to automate the preparation, administration, and evaluation of academic assessments. Developed for the **HU-SENG Comprehensive Pre-Internship Training 2026**, the system serves as a centralized hub for Exam Coordinators to manage high-stakes content and for Students to undergo rigorous digital evaluations with real-time performance feedback.

## 2. Problem Statement
Previously, student evaluations were conducted using traditional paper-based methods, which were prone to manual errors, lacked support for rich media (like code snippets), and suffered from inefficient data entry. HEMS addresses these challenges by providing a **"Unified Exam Entry"** architecture that supports both manual CRUD operations and high-speed ZIP-based bulk ingestion, ensuring data integrity through atomic database transactions.

## 3. Key Functional Requirements

### **Coordinator Module**
* **Secure Authentication**: Role-based access to the administrative dashboard.
* **Student Management (Closed System)**: Bulk import of students via CSV with automated account and credential generation.
* **Unified Question Ingestion**: 
    * **Manual Entry**: Standard CRUD forms for questions and options.
    * **Bulk Ingestion**: ZIP-based service to upload questions, manifests, and images simultaneously.
* **Exam Control**: Ability to publish exams, set durations, and generate unique authorization codes.
* **Automatic Grading**: System-calculated scores based on predefined correct answers.

### **Student Module**
* **Secure Exam Access**: Authentication via system-generated credentials and exam-specific codes.
* **Interactive Exam Interface**: Responsive design with question navigation and a "Flag for Review" feature.
* **Real-time Notifications**: Automated alerts triggered when **15 minutes** and **5 minutes** remain in the session.
* **Instant Results**: Immediate access to performance summaries upon submission.

## 4. Tech Stack
* **Backend**: ASP.NET Core MVC (C#)
* **Database**: Microsoft SQL Server (MS-SQL)
* **ORM**: Entity Framework Core
* **Authentication**: ASP.NET Core Identity
* **Frontend**: Razor Pages, Bootstrap, and **Prism.js** (for code snippet rendering)

## 5. Database Design
The system utilizes a highly normalized relational schema (3rd Normal Form) to ensure data consistency.
* **Core Tables**: AspNetUsers, Students, Exams, Questions, Choices, StudentExams, and ExamAttempts.
* **Rich Media**: The Questions table includes an ImagePath attribute to support image rendering within assessments.

## 6. Setup Instructions
1.  **Clone the repository**: `git clone https://github.com/ThousandGolder/HUHEMS.git`
2.  **Configure Database**: Update the connection string in `appsettings.json` to point to your local MS-SQL Server.
3.  **Apply Migrations**: Run `Update-Database` in the Package Manager Console.
4.  **Run**: Press `F5` or use `dotnet run` in the IDE.
5.  **Note**: Public registration is disabled; use the Coordinator dashboard to import student accounts.
