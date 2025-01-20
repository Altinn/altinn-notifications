const replacements = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    "'": '&#39;',
    '"': '&quot;',
};

const escapeHTML = (str) => str.replace(/[&<>'"]/g, (char) => replacements[char]);

const checksToTestcase = (checks) => {
    let failures = 0;
    const testCases = checks.map((check) => {
        if (check.passes >= 1 && check.fails === 0) {
            return `<testcase name="${escapeHTML(check.name)}"/>`;
        } else {
            failures++;
            return `<testcase name="${escapeHTML(check.name)}"><failure message="failed"/></testcase>`;
        }
    });
    return [testCases, failures];
};

/**
 * Generate a junit xml string from the summary of a k6 run considering each checks as a test case
 * @param {*} data
 * @param {String} suiteName Name of the test ex., filename
 * @returns junit xml string
 */
export const generateJUnitXML = (data, suiteName) => {
    let failures = 0;
    const allTests = [];
    const time = data.state.testRunDurationMs || 0;

    if (data.root_group.groups?.length > 0) {
        data.root_group.groups.forEach((group) => {
            if (group.checks) {
                const [testSubset, groupFailures] = checksToTestcase(group.checks);
                allTests.push(...testSubset);
                failures += groupFailures;
            }
        });
    }

    if (data.root_group.checks) {
        const [testSubset, rootFailures] = checksToTestcase(data.root_group.checks);
        allTests.push(...testSubset);
        failures += rootFailures;
    }

    return (
        `<?xml version="1.0" encoding="UTF-8" ?>\n<testsuites tests="${allTests.length}" ` +
        `failures="${failures}" time="${time}">\n` +
        `<testsuite name="${escapeHTML(suiteName)}" tests="${allTests.length}" failures="${failures}" ` +
        `time="${time}" timestamp="${new Date().toISOString()}">\n` +
        `${allTests.join('\n')}\n</testsuite>\n</testsuites>`
    );
};

/**
 * Returns string that is path to the reports based on the OS where the test in run
 * @param {String} reportName name of the file with extension
 * @returns path
 */
export const reportPath = (reportName) => {
    const basePath = `src/reports/${reportName}`;
    return !(__ENV.OS || __ENV.AGENT_OS) ? `/${basePath}` : basePath;
};
